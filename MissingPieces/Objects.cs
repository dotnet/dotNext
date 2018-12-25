using System;
using System.Runtime.CompilerServices;

namespace MissingPieces
{
	public static class Objects
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static B Upcast<B, D>(this D obj)
			where D: class, B
			=> obj;

		public static bool OneOf<T>(this T value, params T[] values)
			where T: class
		{
			foreach (var v in values)
				if (Equals(value, v))
					return true;
			return false;
		}

		public static (R1, R2) Decompose<T, R1, R2>(this T obj, Func<T, R1> decomposer1, Func<T, R2> decomposer2)
			where T: class
			=> (decomposer1(obj), decomposer2(obj));
	}
}
