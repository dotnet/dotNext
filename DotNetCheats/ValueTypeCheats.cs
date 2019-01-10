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
		/// <summary>
		/// Computes hash code for the structure content.
		/// </summary>
		/// <typeparam name="T">Stucture type.</typeparam>
		/// <param name="value"></param>
		/// <returns>Content hash code.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe int BitwiseHashCode<T>(this T value)
			where T : struct
			=> ValueType<T>.GetHashCode(value);
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe bool BitwiseEquals<T>(this T first, T second)
			where T: struct
			=> ValueType<T>.Equals(first, second);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe int BitwiseCompare<T>(this T first, T second)
			where T: unmanaged
			=> ValueType<T>.Compare(first, second);

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void BitCast<I, O>(this I input, out O output)
			where I: unmanaged
			where O: unmanaged
			=> output = BitCast<I, O>(input);

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
		public static StrongBox<T> Box<T>(this T value)
			where T: struct
			=> new StrongBox<T>(value);
    }
}