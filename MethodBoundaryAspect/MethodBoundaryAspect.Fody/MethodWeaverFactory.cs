using MethodBoundaryAspect.Fody.Ordering;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MethodBoundaryAspect.Fody
{
    public static class MethodWeaverFactory
    {
        public static MethodWeaver MakeWeaver(ModuleDefinition module,
            MethodDefinition method,
            IEnumerable<AspectInfo> aspects,
            MethodInfoCompileTimeWeaver methodInfoCompileTimeWeaver)
        {
            var filteredAspects = from a in aspects
                                  let methods = GetUsedAspectMethods(a.AspectAttribute.AttributeType)
                                  where methods != AspectMethods.None
                                  select new { Aspect = a, Methods = methods };

            var asyncAttribute = method.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName.Equals(typeof(AsyncStateMachineAttribute).FullName));
            
            if (asyncAttribute == null && IsUniTaskAsyncMethod(method))
            {
                var moveNextMethod = FindUniTaskMoveNextMethod(method);
                if (moveNextMethod != null)
                {
                    
                    var aspectDatas = filteredAspects.Select(a => 
                        new AspectDataOnAsyncMethod(moveNextMethod, a.Aspect, a.Methods, method, module)).ToList<AspectData>();
                    
                    return new AsyncMethodWeaver(
                        module,
                        method,
                        moveNextMethod,
                        aspectDatas,
                        methodInfoCompileTimeWeaver);
                }
            }
            
            if (asyncAttribute != null)
            {
                var moveNextMethod =
                    ((TypeDefinition)asyncAttribute.ConstructorArguments[0].Value).Methods.First(m =>
                        m.Name == "MoveNext");

                // Pass the moveNextMethod itself as the generic context provider
                var aspectDatas = filteredAspects.Select(a =>
                        new AspectDataOnAsyncMethod(moveNextMethod, a.Aspect, a.Methods, method, module))
                    .ToList<AspectData>();

                return new AsyncMethodWeaver(
                    module,
                    method,
                    moveNextMethod,
                    aspectDatas,
                    methodInfoCompileTimeWeaver);
            }

            var aspectList = filteredAspects.Select(a => new AspectData(a.Aspect, a.Methods, method, module)).ToList();
            return new MethodWeaver(module, method, aspectList, methodInfoCompileTimeWeaver);
        }

        public static bool IsUniTaskAsyncMethod(MethodDefinition method)
        {
            var returnTypeName = method.ReturnType.FullName;
            
            if (returnTypeName == null || 
                (!returnTypeName.StartsWith("Cysharp.Threading.Tasks.UniTask") &&
                 returnTypeName != "Cysharp.Threading.Tasks.UniTaskVoid"))
            {
                return false;
            }
            
            if (IsSimpleReturnMethod(method))
            {
                return false;
            }
            
            var declaringType = method.DeclaringType;
            bool hasStateMachine = false;
            TypeDefinition stateMachineType = null;
            
            foreach (var nestedType in declaringType.NestedTypes)
            {
                var isCompilerGenerated = nestedType.CustomAttributes.Any(a => a.AttributeType.FullName == typeof(CompilerGeneratedAttribute).FullName);
                var containsMethodName = nestedType.Name.Contains($"<{method.Name}>");
                var hasUniTaskBuilder = HasUniTaskBuilderField(nestedType);
                
                if (containsMethodName && isCompilerGenerated && hasUniTaskBuilder)
                {
                    hasStateMachine = true;
                    stateMachineType = nestedType;
                    break;
                }
            }
            
            if (!hasStateMachine)
            {
                return false;
            }
            
            if (!MethodCreatesStateMachineInstance(method, stateMachineType))
            {
                return false;
            }
            
            return hasStateMachine;
        }
        
        private static bool IsSimpleReturnMethod(MethodDefinition method)
        {
            if (method.Body?.Instructions == null || method.Body.Instructions.Count < 3)
                return false;
                
            var instructions = method.Body.Instructions;
            int callIndex = -1;
            
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].OpCode == OpCodes.Call || instructions[i].OpCode == OpCodes.Callvirt)
                {
                    callIndex = i;
                    break;
                }
            }
            
            if (callIndex == -1) return false;
            
            if (callIndex + 1 < instructions.Count && instructions[callIndex + 1].OpCode == OpCodes.Ret)
            {
                var calledMethod = instructions[callIndex].Operand as MethodReference;
                if (calledMethod != null && 
                    calledMethod.DeclaringType.FullName == method.DeclaringType.FullName &&
                    calledMethod.ReturnType.FullName == method.ReturnType.FullName)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        private static bool MethodCreatesStateMachineInstance(MethodDefinition method, TypeDefinition stateMachineType)
        {
            if (method.Body?.Variables == null || stateMachineType == null)
                return false;
                
            var stateMachineVariable = method.Body.Variables.FirstOrDefault(v => 
                v.VariableType.Resolve() == stateMachineType);
                
            if (stateMachineVariable == null)
                return false;
                
            if (method.Body.Instructions != null)
            {
                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode == OpCodes.Newobj && 
                        instruction.Operand is MethodReference constructor &&
                        constructor.DeclaringType.Resolve() == stateMachineType)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        public static MethodDefinition FindUniTaskMoveNextMethod(MethodDefinition method)
        {
            var declaringType = method.DeclaringType;
            
            foreach (var nestedType in declaringType.NestedTypes)
            {
                if (nestedType.Name.Contains($"<{method.Name}>") && 
                    nestedType.CustomAttributes.Any(a => a.AttributeType.FullName == typeof(CompilerGeneratedAttribute).FullName))
                {
                    var moveNextMethod = nestedType.Methods.FirstOrDefault(m => m.Name == "MoveNext");
                    if (moveNextMethod != null && HasUniTaskBuilderField(nestedType))
                    {
                        return moveNextMethod;
                    }
                }
            }
            
            return null;
        }

        private static bool HasUniTaskBuilderField(TypeDefinition type)
        {
            foreach (var field in type.Fields)
            {
                var fieldTypeName = field.FieldType.FullName;
                if (fieldTypeName != null && 
                    (fieldTypeName.Contains("AsyncUniTaskMethodBuilder") ||
                     fieldTypeName.Contains("AsyncUniTaskVoidMethodBuilder") ||
                     fieldTypeName.Contains("Cysharp.Threading.Tasks")))
                {
                    return true;
                }
            }
            return false;
        }

        static AspectMethods GetUsedAspectMethods(TypeReference aspectTypeDefinition)
        {
            var overloadedMethods = new Dictionary<string, MethodDefinition>();

            var currentType = aspectTypeDefinition;
            do
            {
                var typeDefinition = currentType.Resolve();
                var methods = typeDefinition.Methods
                    .Where(AspectMethodCriteria.MatchesSignature)
                    .ToList();
                foreach (var method in methods)
                {
                    if (overloadedMethods.ContainsKey(method.Name))
                        continue;

                    overloadedMethods.Add(method.Name, method);
                }

                currentType = typeDefinition.BaseType;
            } while (currentType.FullName != AttributeFullNames.OnMethodBoundaryAspect);

            var aspectMethods = AspectMethods.None;
            if (overloadedMethods.ContainsKey(AspectMethodCriteria.OnEntryMethodName))
                aspectMethods |= AspectMethods.OnEntry;
            if (overloadedMethods.ContainsKey(AspectMethodCriteria.OnExitMethodName))
                aspectMethods |= AspectMethods.OnExit;
            if (overloadedMethods.ContainsKey(AspectMethodCriteria.OnExceptionMethodName))
                aspectMethods |= AspectMethods.OnException;
            return aspectMethods;
        }
    }
}
