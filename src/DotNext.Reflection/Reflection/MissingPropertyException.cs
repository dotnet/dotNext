using System;

namespace DotNext.Reflection
{
    /// <summary>
    /// Indicates that requested property doesn't exist.
    /// </summary>
    public sealed class MissingPropertyException : ConstraintViolationException
    {
        /// <summary>
        /// Initializes a new exception indicating that requested property doesn't exist.
        /// </summary>
        /// <param name="declaringType">The inspected type.</param>
        /// <param name="propertyName">The name of the missing property.</param>
        /// <param name="propertyType">The type of the missing property.</param>
        public MissingPropertyException(Type declaringType, string propertyName, Type propertyType)
            : base(declaringType, ExceptionMessages.MissingProperty(propertyName, propertyType, declaringType))
        {
            PropertyType = propertyType;
            PropertyName = propertyName;
        }

        internal static MissingPropertyException Create<T, TValue>(string propertyName)
            => new(typeof(T), propertyName, typeof(TValue));

        /// <summary>
        /// Gets type of the missing property.
        /// </summary>
        public Type PropertyType { get; }

        /// <summary>
        /// Gets name of the missing property.
        /// </summary>
        public string PropertyName { get; }
    }
}