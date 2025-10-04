using MethodBoundaryAspect.Fody.Ordering;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;

namespace MethodBoundaryAspect.Fody
{
    public class AspectDataOnAsyncMethod : AspectData
    {
        TypeReference _stateMachine;
        MethodDefinition _moveNext;
        VariableDefinition _stateMachineLocal;
        FieldReference _tagField;
        FieldReference _aspectField;

        public AspectDataOnAsyncMethod(MethodDefinition moveNext, AspectInfo info, AspectMethods methods, MethodDefinition method, ModuleDefinition module)
            : base(info, methods, method, module)
        {
            _moveNext = moveNext;
            
            // Безопасный поиск state machine переменной
            _stateMachineLocal = method.Body.Variables.FirstOrDefault(v => v.VariableType.Resolve() == moveNext.DeclaringType);
            
            if (_stateMachineLocal == null)
            {
                // Собираем детальную информацию для отладки
                var variables = string.Join(", ", method.Body.Variables.Select((v, i) => $"var{i}:{v.VariableType.FullName}"));
                var nestedTypes = string.Join(", ", method.DeclaringType.NestedTypes.Select(t => t.Name));
                var instructions = string.Join(" -> ", method.Body.Instructions.Take(10).Select(i => $"{i.OpCode}"));
                
                throw new InvalidOperationException($"Could not find state machine variable in method {method.FullName}. " +
                    $"MoveNext declaring type: {moveNext.DeclaringType.FullName}. " +
                    $"Method variables: [{variables}]. " +
                    $"Nested types: [{nestedTypes}]. " +
                    $"Method instructions (first 10): {instructions}. " +
                    $"This suggests the method was incorrectly identified as async or has unexpected structure.");
            }
            
            _stateMachine = _stateMachineLocal.VariableType;
        }

        public override void EnsureTagStorage()
        {
            var systemObject = _referenceFinder.GetTypeReference(typeof(object));
            _tagField = _module.ImportReference(_stateMachine.AddPublicInstanceField(systemObject));

            TagPersistable = new FieldPersistable(new VariablePersistable(_stateMachineLocal), _tagField);
        }

        public override InstructionBlockChain CreateAspectInstance()
        {
            var aspectTypeReference = _module.ImportReference(Info.AspectAttribute.AttributeType);
            _aspectField = _module.ImportReference(_stateMachine.AddPublicInstanceField(aspectTypeReference));

            var loadMachine = new VariablePersistable(_stateMachineLocal).Load(true, false);

            var newObjectAspectBlock = _creator.CreateAndNewUpAspect(Info.AspectAttribute);
            var loadOnStack = new InstructionBlock("Load on stack", Instruction.Create(OpCodes.Ldloc, newObjectAspectBlock.Variable));
            var storeField = new InstructionBlock("Store Field", Instruction.Create(OpCodes.Stfld, _aspectField));

            var newObjectAspectBlockChain = new InstructionBlockChain();
            newObjectAspectBlockChain.Add(loadMachine);
            newObjectAspectBlockChain.Add(newObjectAspectBlock);
            newObjectAspectBlockChain.Add(loadOnStack);
            newObjectAspectBlockChain.Add(storeField);

            AspectPersistable = new FieldPersistable(new VariablePersistable(_stateMachineLocal), _aspectField);
            return newObjectAspectBlockChain;
        }

        public IPersistable GetMoveNextExecutionArgs(IPersistable executionArgs)
        {
            var fieldExecutionArgs = executionArgs as FieldPersistable;
            var sm = StateMachineFromMoveNext;
            return new FieldPersistable(new ThisLoadable(sm), fieldExecutionArgs.Field.AsDefinedOn(sm));
        }

        TypeReference StateMachineFromMoveNext
        {
            get
            {
                if (!_stateMachine.ContainsGenericParameter)
                    return _stateMachine;

                var smType = _stateMachine.Resolve();
                var result = smType.MakeGenericType(smType.GenericParameters.ToArray());
                return result;
            }
        }

        public InstructionBlockChain LoadTagInMoveNext(IPersistable executionArgs)
        {
            var setMethod = _referenceFinder.GetMethodReference(executionArgs.PersistedType,
                    md => md.Name == "set_MethodExecutionTag");
            var sm = StateMachineFromMoveNext;

            return _creator.CallVoidMethod(setMethod, GetMoveNextExecutionArgs(executionArgs),
                new FieldPersistable(new ThisLoadable(sm), _tagField.AsDefinedOn(sm)));
        }

        public InstructionBlockChain CallOnExceptionInMoveNext(IPersistable executionArgs, VariableDefinition exceptionLocal)
        {
            var onExceptionMethodRef = _referenceFinder.GetMethodReference(Info.AspectAttribute.AttributeType,
                AspectMethodCriteria.IsOnExceptionMethod);

            var setMethod = _referenceFinder.GetMethodReference(executionArgs.PersistedType,
                md => md.Name == "set_Exception");

            var setExceptionOnArgsBlock = _creator.CallVoidMethod(setMethod, GetMoveNextExecutionArgs(executionArgs),
                new VariablePersistable(exceptionLocal));

            var sm = StateMachineFromMoveNext;
            var smPersistable = new ThisLoadable(sm);

            var aspectPersistable = new FieldPersistable(smPersistable, _aspectField.AsDefinedOn(sm));
            var callOnExceptionBlock = _creator.CallVoidMethod(onExceptionMethodRef,
                aspectPersistable, GetMoveNextExecutionArgs(executionArgs));

            var callAspectOnExceptionBlockChain = new InstructionBlockChain();
            callAspectOnExceptionBlockChain.Add(setExceptionOnArgsBlock);
            callAspectOnExceptionBlockChain.Add(callOnExceptionBlock);
            return callAspectOnExceptionBlockChain;
        }

        /// <summary>
        /// Создает вызов OnExit в MoveNext методе state machine
        /// </summary>
        public InstructionBlockChain CallOnExitInMoveNext(IPersistable executionArgs, IPersistable returnValue = null)
        {
            var onExitMethodRef = _referenceFinder.GetMethodReference(Info.AspectAttribute.AttributeType,
                AspectMethodCriteria.IsOnExitMethod);

            var sm = StateMachineFromMoveNext;
            var smPersistable = new ThisLoadable(sm);

            var aspectPersistable = new FieldPersistable(smPersistable, _aspectField.AsDefinedOn(sm));

            var callOnExitBlockChain = new InstructionBlockChain();

            // Если есть return value - устанавливаем его в executionArgs
            if (returnValue != null)
            {
                var setReturnValueMethod = _referenceFinder.GetMethodReference(executionArgs.PersistedType,
                    md => md.Name == "set_ReturnValue");

                var setReturnValueBlock = _creator.CallVoidMethod(setReturnValueMethod, GetMoveNextExecutionArgs(executionArgs), returnValue);
                callOnExitBlockChain.Add(setReturnValueBlock);
            }

            // Вызываем OnExit
            var callOnExitBlock = _creator.CallVoidMethod(onExitMethodRef,
                aspectPersistable, GetMoveNextExecutionArgs(executionArgs));
            callOnExitBlockChain.Add(callOnExitBlock);

            return callOnExitBlockChain;
        }
    }
}
