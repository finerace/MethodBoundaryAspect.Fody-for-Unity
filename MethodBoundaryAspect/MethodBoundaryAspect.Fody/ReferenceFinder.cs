using Mono.Cecil;
using Mono.Cecil.Rocks;
using System;
using System.Linq;

namespace MethodBoundaryAspect.Fody
{
    public class ReferenceFinder
    {
        private readonly ModuleDefinition _moduleDefinition;

        public ReferenceFinder(ModuleDefinition moduleDefinition)
        {
            _moduleDefinition = moduleDefinition;
        }

        public MethodReference GetMethodReference(Type declaringType, Func<MethodDefinition, bool> predicate, IGenericParameterProvider context = null)
        {
            return GetMethodReference(GetTypeReference(declaringType), predicate, context);
        }

        public MethodReference GetMethodReference(TypeReference typeReference, Func<MethodDefinition, bool> predicate, IGenericParameterProvider context = null)
        {
            var startTypeDefinition = typeReference.Resolve();
            var currentTypeDefinition = startTypeDefinition;
            MethodDefinition methodDefinition = null;

            do
            {
                methodDefinition = currentTypeDefinition.Methods.FirstOrDefault(predicate);
                if (methodDefinition != null)
                    break;
                currentTypeDefinition = currentTypeDefinition.BaseType?.Resolve();
            } while (currentTypeDefinition != null);

            if (methodDefinition == null)
                throw new InvalidOperationException(
                    $"Could not find a method matching the predicate on type {typeReference.FullName} or its base types.");

            var importedMethodRef = _moduleDefinition.ImportReference(methodDefinition, context);

            if (methodDefinition.DeclaringType.Resolve() == startTypeDefinition)
                return importedMethodRef;

            var importedDeclaringType = _moduleDefinition.ImportReference(typeReference, context);
            
            var finalMethodReference = new MethodReference(
                importedMethodRef.Name,
                importedMethodRef.ReturnType,
                importedDeclaringType)
            {
                HasThis = importedMethodRef.HasThis,
                ExplicitThis = importedMethodRef.ExplicitThis,
                CallingConvention = importedMethodRef.CallingConvention
            };

            foreach (var parameter in importedMethodRef.Parameters)
                finalMethodReference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));

            foreach (var generic_parameter in importedMethodRef.GenericParameters)
                finalMethodReference.GenericParameters.Add(new GenericParameter(generic_parameter.Name, finalMethodReference));

            return finalMethodReference;
        }

        public MethodReference GetConstructorReference(TypeReference typeReference, Func<MethodDefinition, bool> predicate)
        {
            var typeDefinition = typeReference.Resolve();
            var methodDefinition = typeDefinition.GetConstructors().FirstOrDefault(predicate);
            return _moduleDefinition.ImportReference(methodDefinition);
        }

        public TypeReference GetTypeReference(Type type, string netCoreAssemblyHint = null)
        {
            var importedType = _moduleDefinition.ImportReference(type);
            if (importedType is TypeSpecification)
                return importedType;

            var scope = importedType.Scope;
            if (scope.Name != _moduleDefinition.TypeSystem.CoreLibrary.Name)
                scope = _moduleDefinition.TypeSystem.CoreLibrary;

            if (scope.Name == "System.Runtime" && netCoreAssemblyHint != null)
                scope = new AssemblyNameReference(netCoreAssemblyHint,
                    _moduleDefinition.AssemblyReferences.First(mr => mr.Name == "System.Runtime").Version);

            importedType.Scope = scope;
            return importedType;
        }
    }
}