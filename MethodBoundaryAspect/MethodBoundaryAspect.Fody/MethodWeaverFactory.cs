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

            // Проверяем стандартный AsyncStateMachineAttribute
            var asyncAttribute = method.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName.Equals(typeof(AsyncStateMachineAttribute).FullName));
            
            // Если не найден стандартный атрибут, проверяем UniTask метод
            if (asyncAttribute == null && IsUniTaskAsyncMethod(method))
            {
                var moveNextMethod = FindUniTaskMoveNextMethod(method);
                if (moveNextMethod != null)
                {
                    var aspectDatas = filteredAspects.Select(a => new AspectDataOnAsyncMethod(moveNextMethod, a.Aspect, a.Methods, method, module)).ToList<AspectData>();
                    return new AsyncMethodWeaver(
                        module,
                        method,
                        moveNextMethod,
                        aspectDatas,
                        methodInfoCompileTimeWeaver);
            }
            }
            
            // Обработка стандартных async методов
            if (asyncAttribute != null)
            {
            var moveNextMethod = ((TypeDefinition)asyncAttribute.ConstructorArguments[0].Value).Methods.First(m => m.Name == "MoveNext");
            var aspectDatas = filteredAspects.Select(a => new AspectDataOnAsyncMethod(moveNextMethod, a.Aspect, a.Methods, method, module)).ToList<AspectData>();
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

        /// <summary>
        /// Определяет является ли метод async UniTask методом
        /// </summary>
        private static bool IsUniTaskAsyncMethod(MethodDefinition method)
        {
            var returnTypeName = method.ReturnType.FullName;
            
            // Сначала проверяем возвращаемый тип
            if (returnTypeName == null || 
                (!returnTypeName.StartsWith("Cysharp.Threading.Tasks.UniTask") &&
                 returnTypeName != "Cysharp.Threading.Tasks.UniTaskVoid"))
            {
                return false;
            }
            
            // ВАЖНО: Более строгая проверка - анализируем IL код метода
            // Простой метод который только возвращает результат другого метода не должен считаться async
            if (IsSimpleReturnMethod(method))
            {
                return false;
            }
            
            // ВАЖНО: Сначала проверяем наличие state machine типа - это самый надежный способ
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
            
            // Если нет state machine, то это НЕ async метод (просто возвращает UniTask)
            if (!hasStateMachine)
            {
                return false;
            }
            
            // КРИТИЧНО: Проверяем что метод действительно создает экземпляр state machine
            // Если state machine есть, но метод его не создает - это не настоящий async метод
            if (!MethodCreatesStateMachineInstance(method, stateMachineType))
            {
                return false;
            }
            
            // Дополнительная проверка - метод должен содержать await операции  
            // (пока не используется, но может пригодиться для будущих улучшений)
            
            // Если есть state machine, но нет await операций в исходном методе,
            // это может быть случай когда компилятор все-таки создал state machine
            // В таком случае считаем это async методом
            return hasStateMachine;
        }
        
        /// <summary>
        /// Проверяет является ли метод простым методом который только возвращает результат другого метода
        /// </summary>
        private static bool IsSimpleReturnMethod(MethodDefinition method)
        {
            if (method.Body?.Instructions == null || method.Body.Instructions.Count < 3)
                return false;
                
            // Простой паттерн: ldarg.0, [ldarg.1, ldarg.2, ...], call, ret
            var instructions = method.Body.Instructions;
            int callIndex = -1;
            
            // Ищем call/callvirt инструкцию
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].OpCode == OpCodes.Call || instructions[i].OpCode == OpCodes.Callvirt)
                {
                    callIndex = i;
                    break;
                }
            }
            
            if (callIndex == -1) return false;
            
            // Проверяем что после call сразу идет ret
            if (callIndex + 1 < instructions.Count && instructions[callIndex + 1].OpCode == OpCodes.Ret)
            {
                // Это может быть простой return методом
                var calledMethod = instructions[callIndex].Operand as MethodReference;
                if (calledMethod != null && 
                    calledMethod.DeclaringType.FullName == method.DeclaringType.FullName &&
                    calledMethod.ReturnType.FullName == method.ReturnType.FullName)
                {
                    // Это вызов метода того же класса с тем же возвращаемым типом - скорее всего простой wrapper
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Проверяет создает ли метод экземпляр state machine
        /// </summary>
        private static bool MethodCreatesStateMachineInstance(MethodDefinition method, TypeDefinition stateMachineType)
        {
            if (method.Body?.Variables == null || stateMachineType == null)
                return false;
                
            // Проверяем есть ли локальная переменная типа state machine
            var stateMachineVariable = method.Body.Variables.FirstOrDefault(v => 
                v.VariableType.Resolve() == stateMachineType);
                
            // Если нет переменной state machine, то метод его не создает
            if (stateMachineVariable == null)
                return false;
                
            // Дополнительно проверяем что в IL коде есть newobj для state machine
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

        /// <summary>
        /// Находит метод MoveNext для UniTask state machine
        /// </summary>
        private static MethodDefinition FindUniTaskMoveNextMethod(MethodDefinition method)
        {
            // Ищем compiler generated классы в том же типе
            var declaringType = method.DeclaringType;
            
            foreach (var nestedType in declaringType.NestedTypes)
            {
                // Проверяем, что это state machine класс
                if (nestedType.Name.Contains($"<{method.Name}>") && 
                    nestedType.CustomAttributes.Any(a => a.AttributeType.FullName == typeof(CompilerGeneratedAttribute).FullName))
                {
                    // Ищем MoveNext метод
                    var moveNextMethod = nestedType.Methods.FirstOrDefault(m => m.Name == "MoveNext");
                    if (moveNextMethod != null && HasUniTaskBuilderField(nestedType))
                    {
                        return moveNextMethod;
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// Проверяет, содержит ли тип поля UniTask builder'ов
        /// </summary>
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
