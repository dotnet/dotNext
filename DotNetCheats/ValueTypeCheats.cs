using System;
using System.Runtime.CompilerServices;

namespace Cheats
{
	using Runtime.InteropServices;

	/// <summary>
	/// Various extensions for value types.
	/// </summary>
    public static class ValueTypeCheats
    {
		public static unsafe int BitwiseHashCode<T>(this T value, int hash, Func<int, int, int> hashFunction, bool useSalt = true)
			where T : struct
			=> ValueType<T>.GetHashCode(value, hash, hashFunction, useSalt);

		/// <summary>
		/// Computes hash code for the structure content.
		/// </summary>
		/// <typeparam name="T">Stucture type.</typeparam>
		/// <param name="value"></param>
		/// <returns>Content hash code.</returns>
		public static unsafe int BitwiseHashCode<T>(this T value)
			where T : struct
			=> ValueType<T>.GetHashCode(value);

		/// <summary>
		/// Computes bitwise equality between two value types.
		/// </summary>
		/// <param name="first">The first structure to compare.</param>
		/// <param name="second">The second structure to compare.</param>
		/// <typeparam name="T1"></typeparam>
		/// <typeparam name="T2"></typeparam>
		/// <returns></returns>
		public static unsafe bool BitwiseEquals<T1, T2>(this T1 first, T2 second)
			where T1: struct
			where T2: struct
			=> ValueType<T1>.Equals(first, second);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe int BitwiseCompare<T1, T2>(this T1 first, T2 second)
			where T1: unmanaged
			where T2: unmanaged
			=> Memory.Compare(&first, &second, Math.Min(ValueType<T1>.Size, ValueType<T2>.Size));

		/// <summary>
		/// Converts one structure into another without changing any bits.
		/// </summary>
		/// <param name="input"></param>
		/// <typeparam name="I"></typeparam>
		/// <typeparam name="O"></typeparam>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe O BitCast<I, O>(this I input)
			where I : unmanaged
			where O : unmanaged
		{
			if(ValueType<I>.Size >= ValueType<O>.Size)
                return Unsafe.As<I, O>(ref input);
            var output = new O();
			Memory.Copy(&input, &output, ValueType<I>.Size);
			return output;
		}

        public static bool OneOf<T>(this T value, params T[] values)
			where T: struct, IEquatable<T>
		{
			foreach (var v in values)
				if (v.Equals(value))
					return true;
			return false;
        }

		/// <summary>
		/// Create boxed representation of the value type.
		/// </summary>
		/// <param name="value">Value to be placed into heap.</param>
		/// <typeparam name="T">Value type.</typeparam>
		/// <returns>Boxed representation of value type.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Box<T> Box<T>(this T value)
			where T: struct
			=> new Box<T>(value);
    }
}