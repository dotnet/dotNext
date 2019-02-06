using System;

namespace DotNext.Reflection
{
	/// <summary>
	/// Indicates that requested attribute doesn't exist.
	/// </summary>
	public sealed class MissingAttributeException : ConstraintViolationException
	{
		public MissingAttributeException(Type target, Type attributeType)
			: base(target, ExceptionMessages.MissingAttribute(attributeType, target))
		{
			AttributeType = attributeType;
		}

		public Type AttributeType { get; }

		internal static MissingAttributeException Create<T, A>()
			where A : Attribute
			=> new MissingAttributeException(typeof(T), typeof(A));
	}
}
