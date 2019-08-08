using System;

namespace DotNext.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    public abstract class ModifierAttribute : Attribute
    {
        private protected ModifierAttribute(Type modifier)
            => Modifier = modifier;

        public Type Modifier { get; }

        public abstract bool IsRequired { get; }
    }
}
