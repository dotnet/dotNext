using System;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Attaches required modifier to the field, parameter or return type.
    /// </summary>
    /// <remarks>
    /// This attribute will be replaced by <c>modreq</c> IL directive.
    /// </remarks>
    [CLSCompliant(false)]
    public sealed class RequiredModifierAttribute : ModifierAttribute
    {
        /// <summary>
        /// Initializes a new required modifier.
        /// </summary>
        /// <param name="modifier">The type representing modifier.</param>
        public RequiredModifierAttribute(Type modifier)
            : base(modifier)
        {
        }

        /// <summary>
        /// Always returns <see langword="true"/>.
        /// </summary>
        public override bool IsRequired => true;
    }
}
