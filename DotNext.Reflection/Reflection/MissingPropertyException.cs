using System;

namespace Cheats.Reflection
{
	/// <summary>
	/// Indicates that requested property doesn't exist.
	/// </summary>
	public sealed class MissingPropertyException : ConstraintViolationException
	{
		private MissingPropertyException(Type declaringType, string propertyName, Type propertyType)
			: base(declaringType, $"Property {propertyName} of type {propertyType.FullName} doesn't exist in type {declaringType.FullName}")
		{
			PropertyType = propertyType;
			PropertyName = propertyName;
		}

		internal static MissingPropertyException Create<T, P>(string propertyName)
			=> new MissingPropertyException(typeof(T), propertyName, typeof(P));

		public Type PropertyType { get; }
		public string PropertyName { get; }
	}
}