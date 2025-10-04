using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace MethodBoundaryAspect.Fody
{
    /// <summary>
    /// Управляет наследованием атрибутов от родительских методов к анонимным
    /// </summary>
    public static class AttributeInheritanceManager
    {
        /// <summary>
        /// Создает копию атрибута для анонимного метода
        /// </summary>
        public static CustomAttribute CloneAttribute(CustomAttribute originalAttribute, ModuleDefinition module)
        {
            var attributeType = module.ImportReference(originalAttribute.AttributeType);
            var clonedAttribute = new CustomAttribute(originalAttribute.Constructor, originalAttribute.GetBlob());
            
            // Копируем constructor arguments
            foreach (var arg in originalAttribute.ConstructorArguments)
            {
                clonedAttribute.ConstructorArguments.Add(arg);
            }
            
            // Копируем named arguments (properties и fields)
            foreach (var namedArg in originalAttribute.Properties)
            {
                clonedAttribute.Properties.Add(namedArg);
            }
            
            foreach (var namedArg in originalAttribute.Fields)
            {
                clonedAttribute.Fields.Add(namedArg);
            }
            
            return clonedAttribute;
        }

        /// <summary>
        /// Получает все MethodBoundaryAspect атрибуты от родительского метода
        /// </summary>
        public static List<CustomAttribute> GetInheritableAttributes(MethodDefinition parentMethod)
        {
            var inheritableAttributes = new List<CustomAttribute>();
            
            if (parentMethod?.CustomAttributes == null)
                return inheritableAttributes;

            // Собираем все атрибуты которые наследуются от OnMethodBoundaryAspect
            foreach (var attribute in parentMethod.CustomAttributes)
            {
                if (IsMethodBoundaryAspect(attribute))
                {
                    inheritableAttributes.Add(attribute);
                }
            }

            return inheritableAttributes;
        }

        /// <summary>
        /// Проверяет является ли атрибут MethodBoundaryAspect
        /// </summary>
        private static bool IsMethodBoundaryAspect(CustomAttribute customAttribute)
        {
            return IsMethodBoundaryAspect(customAttribute.AttributeType.Resolve());
        }

        /// <summary>
        /// Проверяет является ли тип атрибута MethodBoundaryAspect
        /// </summary>
        private static bool IsMethodBoundaryAspect(TypeDefinition attributeTypeDefinition)
        {
            var currentType = attributeTypeDefinition?.BaseType;
            if (currentType == null)
                return false;

            do
            {
                if (currentType.FullName == AttributeFullNames.OnMethodBoundaryAspect)
                    return true;

                currentType = currentType.Resolve()?.BaseType;
            } while (currentType != null);

            return false;
        }

        /// <summary>
        /// Наследует атрибуты от родительского метода к анонимному
        /// </summary>
        public static void InheritAttributes(MethodDefinition anonymousMethod, MethodDefinition parentMethod, ModuleDefinition module)
        {
            var inheritableAttributes = GetInheritableAttributes(parentMethod);
            
            if (inheritableAttributes.Count == 0)
                return;

            // Проверяем что у анонимного метода еще нет этих атрибутов
            var existingAttributeTypes = new HashSet<string>(
                anonymousMethod.CustomAttributes.Select(attr => attr.AttributeType.FullName));

            foreach (var inheritableAttribute in inheritableAttributes)
            {
                var attributeTypeName = inheritableAttribute.AttributeType.FullName;
                
                // Если атрибут уже есть - не добавляем дубликат
                if (existingAttributeTypes.Contains(attributeTypeName))
                    continue;

                try
                {
                    var clonedAttribute = CloneAttribute(inheritableAttribute, module);
                    anonymousMethod.CustomAttributes.Add(clonedAttribute);
                    
                    // Добавляем в set для предотвращения дубликатов
                    existingAttributeTypes.Add(attributeTypeName);
                }
                catch (System.Exception ex)
                {
                    // Если не удалось склонировать атрибут - пропускаем его
                    // Это может происходить если атрибут имеет сложные параметры
                    System.Diagnostics.Debug.WriteLine($"Failed to clone attribute {attributeTypeName} for anonymous method {anonymousMethod.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Обрабатывает все анонимные методы в compiler-generated классе
        /// </summary>
        public static void ProcessCompilerGeneratedClass(TypeDefinition compilerGeneratedClass, ModuleDefinition module)
        {
            if (!AnonymousMethodParser.IsCompilerGeneratedClass(compilerGeneratedClass))
                return;

            foreach (var method in compilerGeneratedClass.Methods.ToList())
            {
                ProcessAnonymousMethod(method, module);
            }
        }

        /// <summary>
        /// Обрабатывает анонимный метод
        /// </summary>
        public static void ProcessAnonymousMethod(MethodDefinition method, ModuleDefinition module)
        {
            var parentInfo = AnonymousMethodParser.GetParentMethodInfo(method);
            if (parentInfo == null)
                return;

            InheritAttributes(method, parentInfo.ParentMethod, module);
        }

        /// <summary>
        /// Обрабатывает все анонимные методы в типе и его nested типах
        /// </summary>
        public static void ProcessTypeForAnonymousMethods(TypeDefinition type, ModuleDefinition module)
        {
            // Обрабатываем nested классы (compiler-generated)
            if (type.HasNestedTypes)
            {
                foreach (var nestedType in type.NestedTypes.ToList())
                {
                    ProcessCompilerGeneratedClass(nestedType, module);
                    
                    // Рекурсивно обрабатываем вложенные типы
                    ProcessTypeForAnonymousMethods(nestedType, module);
                }
            }
        }
    }
} 