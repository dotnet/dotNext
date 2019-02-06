using System;

namespace DotNext.Reflection
{
	/// <summary>
	/// Indicates that requested field doesn't exist.
	/// </summary>
	public sealed class MissingFieldException : ConstraintViolationException
	{
		private MissingFieldException(Type declaringType, string fieldName, Type fieldType)
			: base(declaringType, ExceptionMessages.MissingField(fieldName, fieldType, declaringType))
		{
			FieldType = fieldType;
			FieldName = fieldName;
		}

		internal static MissingFieldException Create<T, F>(string fieldName)
			=> new MissingFieldException(typeof(T), fieldName, typeof(F));

		public Type FieldType { get; }
		public string FieldName { get; }
	}
}
