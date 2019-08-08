using Mono.Cecil;

namespace DotNext.Runtime.CompilerServices
{
    internal interface IModifierWeaver
    {
        void AttachModifier(TypeReference modifierType, bool required);
    }
}
