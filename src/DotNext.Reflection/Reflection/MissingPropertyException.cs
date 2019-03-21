using System;

namespace DotNext.Reflection
{
	/// <summary>
	/// Indicates that requested property doesn't exist.
	/// </summary>
	public sealed class MissingPropertyException : ConstraintViolationException
	{
		private MissingPropertyException(Type declaringType, string propertyName, Type propertyType)
			: base(declaringType, ExceptionMessages.MissingProperty(propertyName, propertyType, declaringType))
		{
			PropertyType = propertyType;
			PropertyName = propertyName;
		}

		internal static MissingPropertyException Create<T, P>(string propertyName)
			=> new MissingPropertyException(typeof(T), propertyName, typeof(P));

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