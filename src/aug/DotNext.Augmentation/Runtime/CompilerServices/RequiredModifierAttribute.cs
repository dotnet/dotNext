using System;

namespace DotNext.Runtime.CompilerServices
{
    public sealed class RequiredModifierAttribute : ModifierAttribute
    {
        public RequiredModifierAttribute(Type modifier)
            : base(modifier)
        {
        }

        public override bool IsRequired => true;
    }
}
