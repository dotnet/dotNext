using System;

namespace DotNext.Reflection
{
    /// <summary>
    /// Indicates that requested attribute doesn't exist.
    /// </summary>
    public sealed class MissingAttributeException : ConstraintViolationException
    {
        /// <summary>
        /// Initializes a new exception indicating that requested attribute doesn't exist.
        /// </summary>
        /// <param name="target">The inspected type.</param>
        /// <param name="attributeType">The type of missing attribute.</param>
        public MissingAttributeException(Type target, Type attributeType)
            : base(target, ExceptionMessages.MissingAttribute(attributeType, target))
        {
            AttributeType = attributeType;
        }

        /// <summary>
        /// Gets type of missing attribute.
        /// </summary>
        public Type AttributeType { get; }

        internal static MissingAttributeException Create<T, TArgs>()
            where TArgs : Attribute
            => new (typeof(T), typeof(TArgs));
    }
}
