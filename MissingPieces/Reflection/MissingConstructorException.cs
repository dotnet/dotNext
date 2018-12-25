using System;

namespace MissingPieces.Reflection
{
	/// <summary>
	/// Indicates that requested constructor doesn't exist.
	/// </summary>
	public sealed class MissingConstructorException: ConstraintViolationException
	{
		private MissingConstructorException(Type target, params Type[] args)
			: base($"Type {target.FullName} doesn't have constructor with parameters {args.ToString(",")}", target)
		{
		}

		internal static MissingConstructorException Create<T>()
			=> new MissingConstructorException(typeof(T));

		internal static MissingConstructorException Create<T, P>()
			=> new MissingConstructorException(typeof(T), typeof(P));

		internal static MissingConstructorException Create<T, P1, P2>()
			=> new MissingConstructorException(typeof(T), typeof(P1), typeof(P2));

		internal static MissingConstructorException Create<T, P1, P2, P3>()
			=> new MissingConstructorException(typeof(T), typeof(P1), typeof(P2), typeof(P3));

		internal static MissingConstructorException Create<T, P1, P2, P3, P4>()
			=> new MissingConstructorException(typeof(T), typeof(P1), typeof(P2), typeof(P3), typeof(P4));

		internal static MissingConstructorException Create<T, P1, P2, P3, P4, P5>()
			=> new MissingConstructorException(typeof(T), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5));
		
		internal static MissingConstructorException Create<T, P1, P2, P3, P4, P5, P6>()
			=> new MissingConstructorException(typeof(T), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5), typeof(P6));
		
		internal static MissingConstructorException Create<T, P1, P2, P3, P4, P5, P6, P7>()
			=> new MissingConstructorException(typeof(T), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5), typeof(P6), typeof(P7));

		internal static MissingConstructorException Create<T, P1, P2, P3, P4, P5, P6, P7, P8>()
			=> new MissingConstructorException(typeof(T), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5), typeof(P6), typeof(P7), typeof(P8));

		internal static MissingConstructorException Create<T, P1, P2, P3, P4, P5, P6, P7, P8, P9>()
			=> new MissingConstructorException(typeof(T), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5), typeof(P6), typeof(P7), typeof(P8), typeof(P9));
	
		internal static MissingConstructorException Create<T, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10>()
			=> new MissingConstructorException(typeof(T), typeof(P1), typeof(P2), typeof(P3), typeof(P4), typeof(P5), typeof(P6), typeof(P7), typeof(P8), typeof(P9), typeof(P10));
	}
}
