using System;
using System.Runtime.CompilerServices;

namespace DotNext
{
	using Runtime.InteropServices;

	/// <summary>
	/// Various extensions for value types.
	/// </summary>
    public static class ValueTypes
    {
		/// <summary>
		/// Converts one structure into another without changing any bits.
		/// </summary>
		/// <param name="input">A value to convert.</param>
		/// <typeparam name="I">Type of input struct.</typeparam>
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
		public static ValueType<T> Box<T>(this T value)
			where T: struct
			=> new ValueType<T>(value);
    }
}