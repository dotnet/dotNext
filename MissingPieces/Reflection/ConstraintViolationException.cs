using System;

namespace MissingPieces.Reflection
{
	/// <summary>
	/// Root type for all exceptions related to generic constraints.
	/// </summary>
	public abstract class ConstraintViolationException: Exception
	{
		private protected ConstraintViolationException(string message, Type target)
			: base(message)
		{
			Target = target;
		}

		public Type Target { get; }
	}
}
