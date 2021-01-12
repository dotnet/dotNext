using System;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Attaches optional modifier to the field, parameter or return type.
    /// </summary>
    /// <remarks>
    /// This attribute will be replaced by <c>modopt</c> IL directive.
    /// </remarks>
    public sealed class OptionalModifierAttribute : ModifierAttribute
    {
        /// <summary>
        /// Initializes a new optional modifier.
        /// </summary>
        /// <param name="modifier">The type representing modifier.</param>
        public OptionalModifierAttribute(Type modifier)
            : base(modifier)
        {
        }

        /// <summary>
        /// Always returns <see langword="false"/>.
        /// </summary>
        public override bool IsRequired => false;
    }
}
