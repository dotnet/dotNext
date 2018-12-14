using System;

namespace MissingPieces.Metaprogramming
{
	public sealed class MissingConstructorException: ConstraintException
	{
		private MissingConstructorException(Type target, params Type[] args)
			: base($"Type {target.FullName} doesn't have constructor with parameters {args}", target)
		{
		}

		internal static MissingConstructorException Create<T>()
			=> new MissingConstructorException(typeof(T));

		internal static MissingConstructorException Create<T, P>()
			=> new MissingConstructorException(typeof(T), typeof(P));

		internal static MissingConstructorException Create<T, P1, P2>()
			=> new MissingConstructorException(typeof(T), typeof(P1), typeof(P2));
	}
}
