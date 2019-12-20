using Mono.Cecil;
using System.Collections.Generic;

namespace DotNext.Runtime.CompilerServices
{
    internal static class BeforeFieldInitWeaver
    {
        private const string BeforeFieldInitAttribute = "DotNext.Runtime.CompilerServices.BeforeFieldInitAttribute";

        internal static void Process(TypeDefinition type)
        {
            ICollection<CustomAttribute> attributesToRemove = new LinkedList<CustomAttribute>();
            foreach (var attribute in type.CustomAttributes)
                if (attribute.AttributeType.FullName == BeforeFieldInitAttribute && attribute.ConstructorArguments[0].Value is bool beforeFieldInit)
                {
                    type.IsBeforeFieldInit = beforeFieldInit;
                    attributesToRemove.Add(attribute);
                }
            foreach (var attribute in attributesToRemove)
                type.CustomAttributes.Remove(attribute);
        }
    }
}
