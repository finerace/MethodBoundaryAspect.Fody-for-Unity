using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Fody;
using MethodBoundaryAspect.Fody.Ordering;
using Mono.Cecil;
using Mono.Cecil.Pdb;
using Mono.Collections.Generic;

namespace MethodBoundaryAspect.Fody
{
    public class ModuleWeaver : BaseModuleWeaver
    {
        private MethodInfoCompileTimeWeaver _methodInfoCompileTimeWeaver;

        public ModuleWeaver()
        {
        }

        public bool DisableCompileTimeMethodInfos { get; set; }
        
        public int TotalWeavedTypes { get; private set; }
        public int TotalWeavedMethods { get; private set; }
        public int TotalWeavedProperties { get; private set; }

        public List<string> PropertyFilter { get; } = new List<string>();
        public List<string> MethodFilters { get; } = new List<string>();
        public List<string> TypeFilters { get; } = new List<string>();
        
        public override void Execute()
        {
            Execute(ModuleDefinition);
        }

        public override IEnumerable<string> GetAssembliesForScanning()
        {
            yield return "mscorlib";
            yield return "System";
            yield return "System.Runtime";
            yield return "System.Reflection";
            yield return "System.Diagnostics";
            yield return "netstandard";
        }
        
        private string CreateShadowAssemblyPath(string assemblyPath, string prefix)
        {
            var fileInfoSource = new FileInfo(assemblyPath);
            return
                fileInfoSource.DirectoryName
                + Path.DirectorySeparatorChar
                + "_" + prefix + "_"
                + Path.GetFileNameWithoutExtension(fileInfoSource.Name)
                + "_Weaved_"
                + fileInfoSource.Extension.ToLower();
        }

        public string WeaveToShadowFile(string assemblyPath, IAssemblyResolver assemblyResolver)
        {
            var prefix = Environment.TickCount.ToString();
            var shadowAssemblyPath = CreateShadowAssemblyPath(assemblyPath, prefix);
            File.Copy(assemblyPath, shadowAssemblyPath, true);

            var pdbPath = Path.ChangeExtension(assemblyPath, "pdb");
            var shadowPdbPath = CreateShadowAssemblyPath(pdbPath, prefix);

            if (File.Exists(pdbPath))
                File.Copy(pdbPath, shadowPdbPath, true);

            Weave(shadowAssemblyPath, assemblyResolver);
            return shadowAssemblyPath;
        }

        public void Weave(string assemblyPath, IAssemblyResolver assemblyResolver)
        {
            var readerParameters = new ReaderParameters
            {
                ReadSymbols = true,
                SymbolReaderProvider = new PdbReaderProvider(),
				ReadWrite = true,
                AssemblyResolver = assemblyResolver
            };

			using (var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters))
			{
				var module = assemblyDefinition.MainModule;
				Execute(module);

				var writerParameters = new WriterParameters
				{
					WriteSymbols = true,
					SymbolWriterProvider = new PdbWriterProvider()
				};
				assemblyDefinition.Write(writerParameters);
			}
        }

        public void AddClassFilter(string classFilter)
        {
            TypeFilters.Add(classFilter);
        }

        public void AddMethodFilter(string methodFilter)
        {
            MethodFilters.Add(methodFilter);
        }

        public void AddPropertyFilter(string propertyFilter)
        {
            PropertyFilter.Add(propertyFilter);
        }
        
        private void Execute(ModuleDefinition module)
        {
            _methodInfoCompileTimeWeaver = new MethodInfoCompileTimeWeaver(module)
            {
                IsEnabled = !DisableCompileTimeMethodInfos
            };

            var assemblyMethodBoundaryAspects = module.Assembly.CustomAttributes;

            foreach (var type in module.Types.ToList())
            {
                WeaveTypeAndNestedTypes(module, type, assemblyMethodBoundaryAspects, new HashSet<MethodDefinition>());
            }

            _methodInfoCompileTimeWeaver.Finish();
        }

        private void WeaveTypeAndNestedTypes(ModuleDefinition module, TypeDefinition type,
            Collection<CustomAttribute> assemblyMethodBoundaryAspects, HashSet<MethodDefinition> methodsToSkip)
        {
            AttributeInheritanceManager.ProcessTypeForAnonymousMethods(type, module);
            
            WeaveType(module, type, assemblyMethodBoundaryAspects, methodsToSkip);

            if (type.HasNestedTypes)
            {
                var classMethodBoundaryAspects = new Collection<CustomAttribute>();
                foreach (var assemblyAspect in assemblyMethodBoundaryAspects)
                    classMethodBoundaryAspects.Add(assemblyAspect);
                foreach (var classAspect in type.CustomAttributes)
                    classMethodBoundaryAspects.Add(classAspect);

                foreach (var nestedType in type.NestedTypes.ToList())
                    WeaveTypeAndNestedTypes(module, nestedType, classMethodBoundaryAspects, methodsToSkip);
            }
        }

        private void WeaveType(
            ModuleDefinition module, 
            TypeDefinition type, 
            Collection<CustomAttribute> assemblyMethodBoundaryAspects,
            HashSet<MethodDefinition> methodsToSkip)
        {
            var classMethodBoundaryAspects = type.CustomAttributes;

            var propertyGetters = type.Properties
                .Where(x => x.GetMethod != null)
                .ToDictionary(x => x.GetMethod);

            var propertySetters = type.Properties
                .Where(x => x.SetMethod != null)
                .ToDictionary(x => x.SetMethod);

            var weavedAtLeastOneMethod = false;
            foreach (var method in type.Methods.ToList())
            {
                // THE DEFINITIVE FIX:
                // We handle iterators separately. We find the stub, transfer attributes to MoveNext,
                // and then explicitly skip the stub. The recursive weaver will then find MoveNext
                // and weave it with a *regular* MethodWeaver, which is the correct behavior.
                var iteratorAttribute = method.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == typeof(IteratorStateMachineAttribute).FullName);
                if (iteratorAttribute != null)
                {
                    var stateMachineType = (iteratorAttribute.ConstructorArguments[0].Value as TypeReference)?.Resolve();
                    var moveNextMethod = stateMachineType?.Methods.FirstOrDefault(m => m.Name == "MoveNext");
                    if (moveNextMethod != null)
                    {
                        AttributeInheritanceManager.InheritAttributes(moveNextMethod, method, module);
                        methodsToSkip.Add(method); // Weave MoveNext, not the stub.
                    }
                }
                
                if (!IsWeavableMethod(method) || methodsToSkip.Contains(method))
                    continue;

                // For async methods, we add their MoveNext to the skip list to prevent the recursive
                // weaver from processing it directly. The AsyncMethodWeaver will handle it.
                var asyncAttribute = method.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName.Equals(typeof(AsyncStateMachineAttribute).FullName));
                if (asyncAttribute != null)
                {
                    var moveNextMethod = ((TypeReference)asyncAttribute.ConstructorArguments[0].Value).Resolve().Methods.First(m => m.Name == "MoveNext");
                    methodsToSkip.Add(moveNextMethod);
                }
                else if (MethodWeaverFactory.IsUniTaskAsyncMethod(method))
                {
                    var moveNextMethod = MethodWeaverFactory.FindUniTaskMoveNextMethod(method);
                    if (moveNextMethod != null)
                    {
                        methodsToSkip.Add(moveNextMethod);
                    }
                }

                Collection<CustomAttribute> methodMethodBoundaryAspects;

                if (method.IsGetter)
                    methodMethodBoundaryAspects = propertyGetters.ContainsKey(method) ? propertyGetters[method].CustomAttributes : method.CustomAttributes;
                else if (method.IsSetter)
                    methodMethodBoundaryAspects = propertySetters.ContainsKey(method) ? propertySetters[method].CustomAttributes : method.CustomAttributes;
                else
                    methodMethodBoundaryAspects = method.CustomAttributes;

                var methodVisibility = GetMethodVisibility(method);

                var allAspectAttributes = assemblyMethodBoundaryAspects
                    .Concat(classMethodBoundaryAspects)
                    .Concat(methodMethodBoundaryAspects)
                    .Where(IsMethodBoundaryAspect);

                // Group attributes by their type name. If an aspect is defined on multiple levels
                // (e.g., on the class and also inherited onto the method), this will group them together.
                // By taking the 'Last()' from each group, we select the most specific one,
                // thanks to the Concat order (method-level attributes come last).
                var distinctAspectAttributes = allAspectAttributes
                    .GroupBy(attr => attr.AttributeType.FullName)
                    .Select(group => group.Last());

                var aspectInfos = distinctAspectAttributes
                    .Select(x => new AspectInfo(x))
                    .Where(info => info.HasTargetMemberAttribute(methodVisibility))
                    .Where(info => string.IsNullOrEmpty(info.NamespaceFilter) || Regex.IsMatch(type.Namespace, info.NamespaceFilter))
                    .Where(info => string.IsNullOrEmpty(info.TypeNameFilter) || Regex.IsMatch(type.Name, info.TypeNameFilter))
                    .Where(info => string.IsNullOrEmpty(info.MethodNameFilter) || Regex.IsMatch(method.Name, info.MethodNameFilter))
                    .Where(x => !IsSelfWeaving(type, x))
                    .ToList();

                if (aspectInfos.Count == 0)
                    continue;
                
                foreach (var aspectInfo in aspectInfos)
                    aspectInfo.InitOrderIndex(assemblyMethodBoundaryAspects, classMethodBoundaryAspects, methodMethodBoundaryAspects);

                weavedAtLeastOneMethod = WeaveMethod(
                    module,
                    method,
                    aspectInfos,
                    _methodInfoCompileTimeWeaver);
            }   

            if (weavedAtLeastOneMethod)
                TotalWeavedTypes++;
        }

        private static bool IsSelfWeaving(TypeDefinition targetType, AspectInfo aspectInfo)
        {
            return targetType.FullName == aspectInfo.AspectTypeDefinition.FullName;
        }

        private bool WeaveMethod(
            ModuleDefinition module,
            MethodDefinition method,
            List<AspectInfo> aspectInfos,
            MethodInfoCompileTimeWeaver methodInfoCompileTimeWeaver)
        {
            aspectInfos = AspectOrderer.Order(aspectInfos);
            var aspectInfosWithMethods = aspectInfos
                .Where(x => !x.SkipProperties || (!method.IsGetter && !method.IsSetter))
                .ToList();

            var methodWeaver = MethodWeaverFactory.MakeWeaver(module, method, aspectInfosWithMethods, methodInfoCompileTimeWeaver);
            methodWeaver.Weave();
            if (methodWeaver.WeaveCounter == 0)
                return false;

            if (method.IsGetter || method.IsSetter)
                TotalWeavedProperties++;
            else
                TotalWeavedMethods++;

            return true;
        }

        private bool IsMethodBoundaryAspect(TypeDefinition attributeTypeDefinition)
        {
            var currentType = attributeTypeDefinition?.BaseType;
            if (currentType == null)
                return false;

            do
            {
                if (currentType.FullName == AttributeFullNames.OnMethodBoundaryAspect)
                    return true;

                currentType = currentType.Resolve().BaseType;
            } while (currentType != null);

            return false;
        }

        private bool IsMethodBoundaryAspect(CustomAttribute customAttribute)
        {
            return IsMethodBoundaryAspect(customAttribute.AttributeType.Resolve());
        }

        private bool IsWeavableMethod(MethodDefinition method)
        {
            if (IsIgnoredByWeaving(method))
                return false;

            if (IsUserFiltered(method.DeclaringType.FullName, method.Name))
                return false;

            if (!method.HasBody) // Replaces IsDelegate
                return false;
                
            if (method.IsAbstract || method.IsConstructor || method.IsPInvokeImpl)
                return false;

            // THE DEFINITIVE FIX 2:
            // Stricter filtering for compiler-generated types. We only want to weave
            // MoveNext methods or user-defined anonymous methods/lambdas, not other
            // compiler helpers (e.g., for awaiters).
            if (AnonymousMethodParser.IsCompilerGeneratedClass(method.DeclaringType))
            {
                if (AnonymousMethodParser.IsMoveNext(method))
                    return true; // Always weave MoveNext if found.

                if (AnonymousMethodParser.IsAnonymousMethod(method))
                    return true; // Always weave user lambdas/local functions.

                return false; // Skip any other compiler-generated helper methods.
            }

            return true;
        }

        private bool IsUserFiltered(string fullName, string name)
        {
            if (TypeFilters.Any())
            {
                var classFullName = fullName;
                var matched = TypeFilters.Contains(classFullName);
                if (!matched)
                    return true;
            }

            if (MethodFilters.Any())
            {
                var methodFullName = $"{fullName}.{name}";
                var matched = MethodFilters.Contains(methodFullName);
                if (!matched)
                    return true;
            }

            if (PropertyFilter.Any())
            {
                var propertySetterFullName = $"{fullName}.{name}";
                var propertyGetterFullName = $"{fullName}.{name}";
                var matched = PropertyFilter.Contains(propertySetterFullName) ||
                              MethodFilters.Contains(propertyGetterFullName);
                if (!matched)
                    return true;
            }

            return false;
        }

        private static bool IsIgnoredByWeaving(MethodDefinition method)
        {
            if (ContainsDisableWeavingAttribute(method.CustomAttributes))
                return true;

            var currentClass = method.DeclaringType;
            do
            {
                if (ContainsDisableWeavingAttribute(currentClass.CustomAttributes))
                    return true;

                currentClass = currentClass.DeclaringType;
            } while (currentClass != null);

            if (ContainsDisableWeavingAttribute(method.Module.CustomAttributes))
                return true;

            if (ContainsDisableWeavingAttribute(method.Module.Assembly.CustomAttributes))
                return true;

            return false;
        }

        private static bool ContainsDisableWeavingAttribute(IEnumerable<CustomAttribute> customAttributes)
        {
            return customAttributes.Any(x => x.AttributeType.FullName == AttributeFullNames.DisableWeavingAttribute);
        }

        private static MethodAttributes GetMethodVisibility(MethodDefinition method) =>
            method.Attributes & MethodAttributes.MemberAccessMask;
    }
}