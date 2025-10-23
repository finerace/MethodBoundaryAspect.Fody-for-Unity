using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MethodBoundaryAspect.Fody
{
    public class MethodWeaver
    {
        private MethodDefinition _clonedMethod;

        protected readonly ModuleDefinition _module;
        protected readonly MethodDefinition _method;
        protected readonly InstructionBlockChainCreator _creator;
        protected readonly ILProcessor _ilProcessor;
        protected readonly IList<AspectData> _aspects;
        private readonly MethodInfoCompileTimeWeaver _methodInfoCompileTimeWeaver;

        protected bool HasMultipleAspects => _aspects.Count > 1;
        protected IPersistable ExecutionArgs { get; set; }

        public int WeaveCounter { get; private set; }

        public MethodWeaver(
            ModuleDefinition module,
            MethodDefinition method,
            IList<AspectData> aspects,
            MethodInfoCompileTimeWeaver methodInfoCompileTimeWeaver)
        {
            _module = module;
            _method = method;
            _creator = new InstructionBlockChainCreator(method, module);
            _ilProcessor = _method.Body.GetILProcessor();
            _aspects = aspects;
            _methodInfoCompileTimeWeaver = methodInfoCompileTimeWeaver;
        }

        public void Weave()
        {
            if (_aspects.Count == 0)
                return;

            Setup();

            var arguments = _creator.CreateMethodArgumentsArray();
            AddToSetup(arguments);

            WeaveMethodExecutionArgs(arguments);

            SetupAspects();

            var hasReturnValue = !InstructionBlockCreator.IsVoid(_method.ReturnType);
            var returnValue = hasReturnValue
                    ? _creator.CreateVariable(_method.ReturnType)
                    : null;

            WeaveOnEntry(returnValue);
            
            HandleBody(arguments, returnValue?.Variable, out var instructionCallStart, out var instructionCallEnd);
            
            // This instruction marks the point where normal execution continues after the try/catch.
            var continuationInstruction = _ilProcessor.Create(OpCodes.Nop);

            if (_aspects.Any(x => (x.AspectMethods & AspectMethods.OnException) != 0))
            {
                // WeaveOnException is now virtual, allowing AsyncWeaver to override it.
                WeaveOnException(
                    _aspects, 
                    instructionCallStart, 
                    instructionCallEnd, 
                    continuationInstruction, 
                    returnValue);
            }
            else
            {
                // If there's no exception handler, we just need a 'leave' to jump over any potential handler code.
                var tryLeaveInstruction = _ilProcessor.Create(OpCodes.Leave, continuationInstruction);
                _ilProcessor.InsertAfter(instructionCallEnd, tryLeaveInstruction);
            }

            _ilProcessor.Append(continuationInstruction);
            
            // WeaveOnExit returns void. Its return value is no longer used.
            WeaveOnExit(hasReturnValue, returnValue);

            HandleReturnValue(hasReturnValue, returnValue);

            Optimize();
            Finish();
            WeaveCounter++;
        }
        
        protected virtual void WeaveOnException(IList<AspectData> allAspects, Instruction instructionCallStart, Instruction instructionCallEnd, Instruction instructionAfterCall, IPersistable returnValue)
        {
            var body = _method.Body;
            var processor = body.GetILProcessor();
            var continuationInstruction = instructionAfterCall;

            // --- CATCH HANDLER BODY (inserted right after the try block) ---
            var handlerInstructions = new List<Instruction>();

            var handlerStartChain = _creator.SaveThrownException();
            handlerInstructions.AddRange(handlerStartChain.InstructionBlocks.SelectMany(b => b.Instructions));
            var handlerStartInstruction = handlerStartChain.First;

            var loadExceptionChain = _creator.LoadValueOnStack(handlerStartChain);
            handlerInstructions.AddRange(loadExceptionChain.InstructionBlocks.SelectMany(b => b.Instructions));

            var setExceptionChain = _creator.SetMethodExecutionArgsExceptionFromStack(ExecutionArgs);
            handlerInstructions.AddRange(setExceptionChain.InstructionBlocks.SelectMany(b => b.Instructions));

            var rethrowInstruction = processor.Create(OpCodes.Rethrow);
            var continueFlowInstruction = processor.Create(OpCodes.Nop);

            var onExceptionAspects = allAspects.Where(a => (a.AspectMethods & AspectMethods.OnException) != 0).Reverse().ToList();
            foreach (var aspect in onExceptionAspects)
            {
                if (HasMultipleAspects)
                {
                    var loadTagChain = _creator.LoadMethodExecutionArgsTagFromPersistable(ExecutionArgs, aspect.TagPersistable);
                    handlerInstructions.AddRange(loadTagChain.InstructionBlocks.SelectMany(b => b.Instructions));
                }
                var callAspectChain = _creator.CallAspectOnException(aspect, ExecutionArgs);
                handlerInstructions.AddRange(callAspectChain.InstructionBlocks.SelectMany(b => b.Instructions));
                var jumpToContinueChain = new InstructionBlockChain();
                jumpToContinueChain.Add(new InstructionBlock("Branch to Continue", processor.Create(OpCodes.Br, continueFlowInstruction)));
                var flowCheckChain = _creator.IfFlowBehaviorIsAnyOf(args: ExecutionArgs, nextInstruction: rethrowInstruction, thenBody: jumpToContinueChain, behaviors: new[] { 1, 3 });
                handlerInstructions.AddRange(flowCheckChain.InstructionBlocks.SelectMany(b => b.Instructions));
            }

            handlerInstructions.Add(rethrowInstruction);
            handlerInstructions.Add(continueFlowInstruction);
            
            if (returnValue != null)
            {
                var readReturnChain = _creator.ReadReturnValue(ExecutionArgs, returnValue);
                handlerInstructions.AddRange(readReturnChain.InstructionBlocks.SelectMany(b => b.Instructions));
            }

            handlerInstructions.Add(processor.Create(OpCodes.Leave, continuationInstruction));

            // --- INSERTION AND METADATA ---
            var tryLeaveInstruction = processor.Create(OpCodes.Leave, continuationInstruction);
            processor.InsertAfter(instructionCallEnd, tryLeaveInstruction);
            
            var cursor = tryLeaveInstruction;
            foreach (var instruction in handlerInstructions)
            {
                processor.InsertAfter(cursor, instruction);
                cursor = instruction;
            }

            var handler = new ExceptionHandler(ExceptionHandlerType.Catch)
            {
                CatchType = _creator.GetExceptionTypeReference(),
                TryStart = instructionCallStart,
                TryEnd = handlerStartInstruction,
                HandlerStart = handlerStartInstruction,
                HandlerEnd = cursor.Next 
            };
            body.ExceptionHandlers.Add(handler);
        }

        protected virtual void Setup()
        {
            _clonedMethod = CloneMethod(_method);
            _method.DeclaringType.Methods.Add(_clonedMethod);
            ClearMethod(_method);
        }

        private static MethodDefinition CloneMethod(MethodDefinition method)
        {
            var targetMethodName = "$_executor_" + method.Name;
            var isStaticMethod = method.IsStatic;
            var methodAttributes = MethodAttributes.Private;
            if (isStaticMethod)
                methodAttributes |= MethodAttributes.Static;

            var clonedMethod = new MethodDefinition(targetMethodName, methodAttributes, method.ReturnType)
            {
                AggressiveInlining = true,
                HasThis = method.HasThis,
                ExplicitThis = method.ExplicitThis,
                CallingConvention = method.CallingConvention
            };

            foreach (var parameter in method.Parameters)
                clonedMethod.Parameters.Add(parameter);

            foreach (var variable in method.Body.Variables)
                clonedMethod.Body.Variables.Add(variable);

            foreach (var variable in method.Body.ExceptionHandlers)
                clonedMethod.Body.ExceptionHandlers.Add(variable);

            var targetProcessor = clonedMethod.Body.GetILProcessor();
            foreach (var instruction in method.Body.Instructions)
                targetProcessor.Append(instruction);

            if (method.HasGenericParameters)
            {
                foreach (var parameter in method.GenericParameters)
                {
                    var clonedParameter = new GenericParameter(parameter.Name, clonedMethod);
                    if (parameter.HasConstraints)
                    {
                        foreach (var parameterConstraint in parameter.Constraints)
                        {
                            clonedParameter.Attributes = parameter.Attributes;
                            clonedParameter.Constraints.Add(parameterConstraint);
                        }
                    }

                    if (parameter.HasReferenceTypeConstraint)
                    {
                        clonedParameter.Attributes |= GenericParameterAttributes.ReferenceTypeConstraint;
                        clonedParameter.HasReferenceTypeConstraint = true;
                    }

                    if (parameter.HasNotNullableValueTypeConstraint)
                    {
                        clonedParameter.Attributes |= GenericParameterAttributes.NotNullableValueTypeConstraint;
                        clonedParameter.HasNotNullableValueTypeConstraint = true;
                    }

                    if (parameter.HasDefaultConstructorConstraint)
                    {
                        clonedParameter.Attributes |= GenericParameterAttributes.DefaultConstructorConstraint;
                        clonedParameter.HasDefaultConstructorConstraint = true;
                    }

                    clonedMethod.GenericParameters.Add(clonedParameter);
                }
            }

            if (method.DebugInformation.HasSequencePoints)
            {
                foreach (var sequencePoint in method.DebugInformation.SequencePoints)
                    clonedMethod.DebugInformation.SequencePoints.Add(sequencePoint);
            }

            clonedMethod.DebugInformation.Scope = new ScopeDebugInformation(method.Body.Instructions.First(), method.Body.Instructions.Last());

            if (method.DebugInformation?.Scope?.Variables != null)
            {
                foreach (var variableDebugInformation in method.DebugInformation.Scope.Variables)
                {
                    clonedMethod.DebugInformation.Scope.Variables.Add(variableDebugInformation);
                }
            }

            return clonedMethod;
        }

        private static void ClearMethod(MethodDefinition method)
        {
            var body = method.Body;
            body.Variables.Clear();
            body.Instructions.Clear();
            body.ExceptionHandlers.Clear();
        }

        protected virtual void AddToSetup(InstructionBlockChain chain)
        {
            chain.Append(_ilProcessor);
        }

        protected virtual void WeaveMethodExecutionArgs(NamedInstructionBlockChain arguments)
        {
            var executionArgs = _creator.CreateMethodExecutionArgsInstance(
                arguments,
                _aspects[0].Info.AspectAttribute.AttributeType,
                _method,
                _methodInfoCompileTimeWeaver);
            AddToSetup(executionArgs);
            ExecutionArgs = executionArgs;
        }

        private void SetupAspects()
        {
            foreach (var aspect in _aspects)
            {
                var instance = aspect.CreateAspectInstance();
                AddToSetup(instance);

                if (HasMultipleAspects)
                    aspect.EnsureTagStorage();
            }
        }

        private void WeaveOnEntry(IPersistable returnValue)
        {
            var aspectsWithOnEntry = _aspects
                .Select((asp, index)=> new { aspect = asp, index })
                .Where(x => (x.aspect.AspectMethods & AspectMethods.OnEntry) != 0)
                .ToList();
            foreach (var entry in aspectsWithOnEntry)
            {
                var aspect = entry.aspect;
                var call = _creator.CallAspectOnEntry(aspect, ExecutionArgs);
                AddToSetup(call);

                if (HasMultipleAspects)
                    AddToSetup(_creator.SaveMethodExecutionArgsTagToPersistable(ExecutionArgs, aspect.TagPersistable));
                
                var nopChain = new InstructionBlockChain();
                nopChain.Add(new InstructionBlock(null, Instruction.Create(OpCodes.Nop)));

                var flowChain = new InstructionBlockChain();
                var onExitChain = new InstructionBlockChain();

                if (_method.ReturnType.IsByReference)
                {
                    var notSupportedExceptionCtorString =
                        _module.ImportReference(
                            _creator.GetExceptionTypeReference<NotSupportedException>()
                                .Resolve().Methods
                                .FirstOrDefault(m => m.IsConstructor && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.String"));
                    onExitChain.Add(new InstructionBlock("Throw NotSupported",
                        Instruction.Create(OpCodes.Ldstr, "Weaving early return from a method with a byref return type is not supported."),
                        Instruction.Create(OpCodes.Newobj, notSupportedExceptionCtorString),
                        Instruction.Create(OpCodes.Throw)));
                }
                else
                {
                    for (var i = entry.index; i >= 0; --i)
                    {
                        var onExitAspect = _aspects[i];
                        if ((onExitAspect.AspectMethods & AspectMethods.OnExit) == 0) 
                            continue;

                        if (HasMultipleAspects)
                            onExitChain.Add(_creator.LoadMethodExecutionArgsTagFromPersistable(ExecutionArgs, onExitAspect.TagPersistable));
                        onExitChain.Add(_creator.CallAspectOnExit(onExitAspect, ExecutionArgs));
                    }
                }

                if (returnValue != null)
                {
                    onExitChain.Add(_creator.ReadReturnValue(ExecutionArgs, returnValue));
                    onExitChain.Add(returnValue.Load(false, false));
                }

                onExitChain.Add(new InstructionBlock("Return", Instruction.Create(OpCodes.Ret)));

                flowChain.Add(_creator.IfFlowBehaviorIsAnyOf(ExecutionArgs, nopChain.First, onExitChain, 3));

                flowChain.Add(nopChain);
                AddToSetup(flowChain);
            }
        }
        
        protected virtual void HandleBody(
            NamedInstructionBlockChain arguments,
            VariableDefinition returnValue,
            out Instruction instructionCallStart,
            out Instruction instructionCallEnd)
        {
            VariableDefinition thisVariable = null;
            if (!_method.IsStatic)
            {
                var thisVariableBlock = _creator.CreateThisVariable(_method.DeclaringType);
                thisVariableBlock.Append(_ilProcessor);
                thisVariable = thisVariableBlock.Variable;
            }

            InstructionBlockChain callSourceMethod;

            ILoadable[] args = null;
            var allowChangingInputArguments = _aspects.Any(x => x.Info.AllowChangingInputArguments);
            if (allowChangingInputArguments)
            {
                args = _method.Parameters
                    .Select((x, i) => new ArrayElementLoadable(arguments.Variable, i, x, _method.Body.GetILProcessor(), _creator))
                    .Cast<ILoadable>()
                    .ToArray();

                callSourceMethod = _creator.CallMethodWithReturn(
                    _clonedMethod,
                    thisVariable == null ? null : new VariablePersistable(thisVariable),
                    returnValue == null ? null : new VariablePersistable(returnValue),
                    args);
            }
            else
            {
                callSourceMethod = _creator.CallMethodWithLocalParameters(
                    _method,
                    _clonedMethod,
                    thisVariable == null ? null : new VariablePersistable(thisVariable),
                    returnValue == null ? null : new VariablePersistable(returnValue));
            }
            
            if (allowChangingInputArguments)
            {
                var copyBackInstructions = new List<Instruction>();
                foreach (var parameter in _method.Parameters.Where(x => x.ParameterType.IsByReference))
                {
                    var arg = args[parameter.Index];
                    copyBackInstructions.Add(_ilProcessor.Create(OpCodes.Ldarg, parameter));

                    var loadBlock = arg.Load(false, true);
                    copyBackInstructions.AddRange(loadBlock.Instructions);

                    var storeOpCode = parameter.ParameterType.MetadataType.GetStIndCode();
                    copyBackInstructions.Add(_ilProcessor.Create(storeOpCode));
                }

                if (copyBackInstructions.Any())
                {
                    var copyBackBlock = new InstructionBlock("Copy back ref values", copyBackInstructions);
                    callSourceMethod.Add(copyBackBlock);
                }
            }

            callSourceMethod.Append(_ilProcessor);
            instructionCallStart = callSourceMethod.First;
            instructionCallEnd = callSourceMethod.Last;
        }

        protected virtual void WeaveOnExit(bool hasReturnValue, NamedInstructionBlockChain returnValue)
        {
            var onExitAspects = _aspects
                            .Where(x => x.AspectMethods.HasFlag(AspectMethods.OnExit))
                            .Reverse()
                            .ToList();
            
            // FIX: Removed the logic that stored the return value as it is now handled by the main Weave method structure.
            // instructionAfterCall = setMethodExecutionArgsReturnValue.First;

            if (hasReturnValue && onExitAspects.Any())
            {
                var loadReturnValue = _creator.LoadValueOnStack(returnValue);

                var setMethodExecutionArgsReturnValue = _creator.SetMethodExecutionArgsReturnValue(
                    ExecutionArgs,
                    loadReturnValue);
                AddToEnd(setMethodExecutionArgsReturnValue);
            }

            foreach (var aspect in onExitAspects)
            {
                if (HasMultipleAspects)
                    AddToEnd(_creator.LoadMethodExecutionArgsTagFromPersistable(ExecutionArgs, aspect.TagPersistable));
                
                AddToEnd(_creator.CallAspectOnExit(aspect, ExecutionArgs));
            }

            if (hasReturnValue && onExitAspects.Any())
                _creator.ReadReturnValue(ExecutionArgs, returnValue).Append(_ilProcessor);
        }

        private void HandleReturnValue(bool hasReturnValue, NamedInstructionBlockChain returnValue)
        {
            if (hasReturnValue)
                AddToEnd(_creator.LoadValueOnStack(returnValue));
            
            AddToEnd(_creator.CreateReturn());
        }

        private void Optimize()
        {
            var attributeCtor = _creator.GetDebuggerStepThroughAttributeCtorReference();

            if (_method.CustomAttributes.All(a => a.AttributeType.FullName != attributeCtor.DeclaringType.FullName))
                _method.CustomAttributes.Add(new CustomAttribute(attributeCtor));

            _method.Body.InitLocals = true;
            _method.Body.Optimize();
            Catel.Fody.CecilExtensions.UpdateDebugInfo(_method);
        }

        protected virtual void Finish()
        {
            _clonedMethod.Body.InitLocals = true;
            _clonedMethod.Body.Optimize();
        }
        
        private void AddToEnd(InstructionBlockChain chain)
        {
            chain.Append(_ilProcessor);
        }
    }
}