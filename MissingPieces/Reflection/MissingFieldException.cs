using System;

namespace MissingPieces.Reflection
{
	/// <summary>
	/// Indicates that requested field doesn't exist.
	/// </summary>
	public sealed class MissingFieldException : ConstraintViolationException
	{
		private MissingFieldException(Type declaringType,
			string fieldName,
			Type fieldType)
			: base($"Field {fieldName} of type {fieldType.FullName} doesn't exist in type {declaringType.FullName}", declaringType)
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
