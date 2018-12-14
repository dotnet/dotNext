using System;

namespace MissingPieces.Metaprogramming
{
	/// <summary>
	/// Root type for all exceptions related to generic constraints.
	/// </summary>
	public abstract class ConstraintException: Exception
	{
		private protected ConstraintException(string message, Type target)
			: base(message)
		{
			Target = target;
		}

		public Type Target { get; }
	}
}
