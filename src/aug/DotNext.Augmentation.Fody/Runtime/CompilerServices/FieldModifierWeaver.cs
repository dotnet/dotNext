using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace DotNext.Runtime.CompilerServices
{
    internal readonly struct FieldModifierWeaver : IModifierWeaver
    {
        private readonly FieldDefinition field;

        internal FieldModifierWeaver(FieldDefinition field) => this.field = field;

        void IModifierWeaver.AttachModifier(TypeReference modifierType, bool required)
        {
            var type = field.FieldType;
            if (required)
                type = type.MakeRequiredModifierType(modifierType);
            else
                type = type.MakeOptionalModifierType(modifierType);
            field.FieldType = type;
        }
    }
}
