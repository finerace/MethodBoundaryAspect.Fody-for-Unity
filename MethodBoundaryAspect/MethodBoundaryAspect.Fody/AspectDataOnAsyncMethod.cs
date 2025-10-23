using System;
using System.Linq;
using MethodBoundaryAspect.Fody.Ordering;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace MethodBoundaryAspect.Fody
{
    public class AspectDataOnAsyncMethod : AspectData
    {
        private readonly TypeReference _stateMachine;
        private MethodDefinition _moveNext;
        private readonly VariableDefinition _stateMachineLocal;
        private FieldReference _tagField;
        private FieldReference _aspectField;

        public AspectDataOnAsyncMethod(MethodDefinition moveNext, AspectInfo info, AspectMethods methods, MethodDefinition method, ModuleDefinition module)
            : base(info, methods, method, module, moveNext)
        {
            _moveNext = moveNext;

            _stateMachineLocal =
                method.Body.Variables.FirstOrDefault(v => v.VariableType.Resolve() == moveNext.DeclaringType);

            if (_stateMachineLocal == null)
            {
                var variables = string.Join(", ",
                    method.Body.Variables.Select((v, i) => $"var{i}:{v.VariableType.FullName}"));
                var nestedTypes = string.Join(", ", method.DeclaringType.NestedTypes.Select(t => t.Name));
                var instructions = string.Join(" -> ", method.Body.Instructions.Take(10).Select(i => $"{i.OpCode}"));

                throw new InvalidOperationException(
                    $"Could not find state machine variable in method {method.FullName}. " +
                    $"MoveNext declaring type: {moveNext.DeclaringType.FullName}. " +
                    $"Method variables: [{variables}]. " +
                    $"Nested types: [{nestedTypes}]. " +
                    $"Method instructions (first 10): {instructions}. " +
                    "This suggests the method was incorrectly identified as async or has unexpected structure.");
            }

            _stateMachine = _stateMachineLocal.VariableType;
        }

        public override void EnsureTagStorage()
        {
            var systemObject = _referenceFinder.GetTypeReference(typeof(object));

            var stateMachineTypeDef = _stateMachine.Resolve();
            var tagFieldDef = stateMachineTypeDef.AddPublicInstanceFieldDefinition(systemObject);

            var tagFieldRef = new FieldReference(tagFieldDef.Name, tagFieldDef.FieldType, _stateMachine);
    
            _tagField = _module.ImportReference(tagFieldRef, _context);

            TagPersistable = new FieldPersistable(new VariablePersistable(_stateMachineLocal), _tagField);
        }

        public override InstructionBlockChain CreateAspectInstance()
        {
            var aspectTypeReference = _module.ImportReference(Info.AspectAttribute.AttributeType);

            var stateMachineTypeDef = _stateMachine.Resolve();
            var aspectFieldDef = stateMachineTypeDef.AddPublicInstanceFieldDefinition(aspectTypeReference);

            var aspectFieldRef = new FieldReference(aspectFieldDef.Name, aspectFieldDef.FieldType, _stateMachine);
    
            _aspectField = _module.ImportReference(aspectFieldRef, _context);

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

            return new FieldPersistable(new ThisLoadable(_stateMachine),
                fieldExecutionArgs.Field.AsDefinedOn(_stateMachine));
        }

        public InstructionBlockChain LoadTagInMoveNext(IPersistable executionArgs)
        {
            // FIX: Pass the generic context (_context) to the reference finder.
            var setMethod = _referenceFinder.GetMethodReference(executionArgs.PersistedType,
                md => md.Name == "set_MethodExecutionTag", _context);

            var smPersistable = new ThisLoadable(_stateMachine);
            var tagPersistable = new FieldPersistable(smPersistable, _tagField.AsDefinedOn(_stateMachine));

            return _creator.CallVoidMethod(setMethod, GetMoveNextExecutionArgs(executionArgs), tagPersistable);
        }


        public InstructionBlockChain CallOnExceptionInMoveNext(IPersistable executionArgs,
            VariableDefinition exceptionLocal)
        {
            // FIX: Pass the generic context (_context) to the reference finder.
            var onExceptionMethodRef = _referenceFinder.GetMethodReference(Info.AspectAttribute.AttributeType,
                AspectMethodCriteria.IsOnExceptionMethod, _context);

            // FIX: Pass the generic context (_context) to the reference finder.
            var setMethod = _referenceFinder.GetMethodReference(executionArgs.PersistedType,
                md => md.Name == "set_Exception", _context);

            var setExceptionOnArgsBlock = _creator.CallVoidMethod(setMethod, GetMoveNextExecutionArgs(executionArgs),
                new VariablePersistable(exceptionLocal));

            var smPersistable = new ThisLoadable(_stateMachine);
            var aspectPersistable = new FieldPersistable(smPersistable, _aspectField.AsDefinedOn(_stateMachine));

            var callOnExceptionBlock = _creator.CallVoidMethod(onExceptionMethodRef,
                aspectPersistable, GetMoveNextExecutionArgs(executionArgs));

            var callAspectOnExceptionBlockChain = new InstructionBlockChain();
            callAspectOnExceptionBlockChain.Add(setExceptionOnArgsBlock);
            callAspectOnExceptionBlockChain.Add(callOnExceptionBlock);
            return callAspectOnExceptionBlockChain;
        }

        public InstructionBlockChain CallOnExitInMoveNext(IPersistable executionArgs, IPersistable returnValue = null)
        {
            // FIX: Pass the generic context (_context) to the reference finder.
            var onExitMethodRef = _referenceFinder.GetMethodReference(Info.AspectAttribute.AttributeType,
                AspectMethodCriteria.IsOnExitMethod, _context);

            var smPersistable = new ThisLoadable(_stateMachine);
            var aspectPersistable = new FieldPersistable(smPersistable, _aspectField.AsDefinedOn(_stateMachine));

            var callOnExitBlockChain = new InstructionBlockChain();

            if (returnValue != null)
            {
                // FIX: Pass the generic context (_context) to the reference finder.
                var setReturnValueMethod = _referenceFinder.GetMethodReference(executionArgs.PersistedType,
                    md => md.Name == "set_ReturnValue", _context);

                var setReturnValueBlock = _creator.CallVoidMethod(setReturnValueMethod,
                    GetMoveNextExecutionArgs(executionArgs), returnValue);
                callOnExitBlockChain.Add(setReturnValueBlock);
            }

            var callOnExitBlock = _creator.CallVoidMethod(onExitMethodRef,
                aspectPersistable, GetMoveNextExecutionArgs(executionArgs));
            callOnExitBlockChain.Add(callOnExitBlock);

            return callOnExitBlockChain;
        }
    }
}