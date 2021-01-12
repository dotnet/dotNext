using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace DotNext.Runtime.CompilerServices
{
    internal readonly struct ReturnTypeModifierWeaver : IModifierWeaver
    {
        private readonly MethodReturnType returnType;

        internal ReturnTypeModifierWeaver(MethodReturnType rtype) => returnType = rtype;

        void IModifierWeaver.AttachModifier(TypeReference modifierType, bool required)
        {
            var type = returnType.ReturnType;
            if (required)
                type = type.MakeRequiredModifierType(modifierType);
            else
                type = type.MakeOptionalModifierType(modifierType);
            returnType.ReturnType = type;
        }
    }
}
