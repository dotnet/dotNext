using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace DotNext.Runtime.CompilerServices
{
    internal readonly struct ParameterModifierWeaver : IModifierWeaver
    {
        private readonly ParameterDefinition parameter;

        internal ParameterModifierWeaver(ParameterDefinition parameter)
            => this.parameter = parameter;

        void IModifierWeaver.AttachModifier(TypeReference modifierType, bool required)
        {
            var type = parameter.ParameterType;
            if (required)
                type = type.MakeRequiredModifierType(modifierType);
            else
                type = type.MakeOptionalModifierType(modifierType);
            parameter.ParameterType = type;
        }
    }
}
