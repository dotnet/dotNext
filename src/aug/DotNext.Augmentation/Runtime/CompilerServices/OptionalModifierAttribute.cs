using System;

namespace DotNext.Runtime.CompilerServices
{
    public sealed class OptionalModifierAttribute : ModifierAttribute
    {
        public OptionalModifierAttribute(Type modifier)
            : base(modifier)
        {
        }

        public override bool IsRequired => false;
    }
}
