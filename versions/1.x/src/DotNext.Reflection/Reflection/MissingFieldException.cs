using System;

namespace DotNext.Reflection
{
    /// <summary>
    /// Indicates that requested field doesn't exist.
    /// </summary>
    public sealed class MissingFieldException : ConstraintViolationException
    {
        /// <summary>
        /// Initializes a new exception indicating that requested field doesn't exist.
        /// </summary>
        /// <param name="declaringType">The inspected type.</param>
        /// <param name="fieldName">The name of the missing field.</param>
        /// <param name="fieldType">The type of the missing field.</param>
        public MissingFieldException(Type declaringType, string fieldName, Type fieldType)
            : base(declaringType, ExceptionMessages.MissingField(fieldName, fieldType, declaringType))
        {
            FieldType = fieldType;
            FieldName = fieldName;
        }

        internal static MissingFieldException Create<T, F>(string fieldName)
            => new MissingFieldException(typeof(T), fieldName, typeof(F));

        /// <summary>
        /// Gets type of the field.
        /// </summary>
		public Type FieldType { get; }

        /// <summary>
        /// Gets name of the missing field.
        /// </summary>
		public string FieldName { get; }
    }
}
