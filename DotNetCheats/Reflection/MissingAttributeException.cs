using System;

namespace MissingPieces.Reflection
{
	/// <summary>
	/// Indicates that requested attribute doesn't exist.
	/// </summary>
	public sealed class MissingAttributeException : ConstraintViolationException
	{
		public MissingAttributeException(Type target, Type attributeType)
			: base(target, $"Attribute {attributeType.FullName} is not defined for type {target.FullName}")
		{
			AttributeType = attributeType;
		}

		public Type AttributeType { get; }

		internal static MissingAttributeException Create<T, A>()
			where A : Attribute
			=> new MissingAttributeException(typeof(T), typeof(A));
	}
}
