using System;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Represents modifier that can be attached to the parameter, field or return type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    public abstract class ModifierAttribute : Attribute
    {
        private protected ModifierAttribute(Type modifier)
            => Modifier = modifier;

        /// <summary>
        /// Gets the modifier type.
        /// </summary>
        public Type Modifier { get; }

        /// <summary>
        /// Indicates that the modifier is required.
        /// </summary>
        public abstract bool IsRequired { get; }
    }
}
