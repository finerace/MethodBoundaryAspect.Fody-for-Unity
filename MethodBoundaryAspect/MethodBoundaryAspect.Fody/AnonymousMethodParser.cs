using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;

namespace MethodBoundaryAspect.Fody
{
    /// <summary>
    /// Парсер имен анонимных методов для определения родительского метода
    /// </summary>
    public static class AnonymousMethodParser
    {
        /// <summary>
        /// Проверяет является ли метод анонимным
        /// </summary>
        public static bool IsAnonymousMethod(MethodDefinition method)
        {
            return method.Name.StartsWith("<") && method.Name.Contains(">");
        }

        /// <summary>
        /// Проверяет является ли класс compiler-generated (содержит анонимные методы)
        /// </summary>
        public static bool IsCompilerGeneratedClass(TypeDefinition type)
        {
            return type.CustomAttributes.Any(attr => 
                attr.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        }

        /// <summary>
        /// Извлекает имя родительского метода из имени анонимного метода
        /// Поддерживает различные паттерны:
        /// - Lambda expressions: <MethodName>b__1_0
        /// - Local functions: <MethodName>g__LocalFuncName|0_0
        /// - Anonymous delegates: <MethodName>b__0
        /// </summary>
        public static string ExtractParentMethodName(string anonymousMethodName)
        {
            if (string.IsNullOrEmpty(anonymousMethodName) || !anonymousMethodName.StartsWith("<"))
                return null;

            // Паттерны для различных типов анонимных методов
            var patterns = new[]
            {
                @"^<(.+?)>b__\d+(_\d+)?$",      // Lambda: <MethodName>b__1_0
                @"^<(.+?)>g__[^|]+\|\d+_\d+$",  // Local function: <MethodName>g__LocalFunc|0_0
                @"^<(.+?)>m__\w+$",             // Anonymous method: <MethodName>m__0
                @"^<(.+?)>b__\d+$"              // Simple lambda: <MethodName>b__0
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(anonymousMethodName, pattern);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Проверяет является ли класс display class для анонимных методов
        /// </summary>
        public static bool IsDisplayClass(TypeDefinition type)
        {
            return type.Name.Contains("DisplayClass") || 
                   type.Name.StartsWith("<>c__") || 
                   type.Name.StartsWith("<>c");
        }

        /// <summary>
        /// Находит родительский метод в declaring type по имени
        /// </summary>
        public static MethodDefinition FindParentMethod(TypeDefinition declaringType, string parentMethodName)
        {
            if (string.IsNullOrEmpty(parentMethodName))
                return null;

            // Сначала ищем в том же классе
            var parentMethod = declaringType.Methods.FirstOrDefault(m => m.Name == parentMethodName);
            if (parentMethod != null)
                return parentMethod;

            // Если не найден и это nested class, ищем в родительском классе
            var currentType = declaringType;
            while (currentType.DeclaringType != null)
            {
                currentType = currentType.DeclaringType;
                parentMethod = currentType.Methods.FirstOrDefault(m => m.Name == parentMethodName);
                if (parentMethod != null)
                    return parentMethod;
            }

            return null;
        }
        
        public static bool IsMoveNext(MethodDefinition method)
        {
            return method.Name == "MoveNext" &&
                   method.DeclaringType.Interfaces.Any(i =>
                       i.InterfaceType.FullName == "System.Collections.IEnumerator");
        }
        
        public static ParentMethodInfo GetParentMethodInfo(MethodDefinition anonymousMethod)
        {
            if (!IsAnonymousMethod(anonymousMethod))
                return null;

            var parentMethodName = ExtractParentMethodName(anonymousMethod.Name);
            if (string.IsNullOrEmpty(parentMethodName))
                return null;

            var parentMethod = FindParentMethod(anonymousMethod.DeclaringType, parentMethodName);
            if (parentMethod == null)
                return null;

            return new ParentMethodInfo
            {
                ParentMethod = parentMethod,
                ParentMethodName = parentMethodName,
                AnonymousMethod = anonymousMethod
            };
        }
    }

    /// <summary>
    /// Информация о родительском методе для анонимного метода
    /// </summary>
    public class ParentMethodInfo
    {
        public MethodDefinition ParentMethod { get; set; }
        public string ParentMethodName { get; set; }
        public MethodDefinition AnonymousMethod { get; set; }
    }
} 