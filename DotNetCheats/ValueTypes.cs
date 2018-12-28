using System;
using System.Runtime.CompilerServices;
using System.Linq;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNetCheats
{
	/// <summary>
	/// Various extensions for value types.
	/// </summary>
    public static class ValueTypes
    {
		public static unsafe int BitwiseHashCode<T>(this T value, int intialHash, Func<int, int, int> hashFunction)
			where T : struct
		{
			var pointer = new IntPtr(AsPointer(ref value));
			for (var size = SizeOf<T>(); size > 0;)
				if(size > sizeof(int))
				{
					intialHash = hashFunction(intialHash, pointer.Read<int>());
					size -= sizeof(int);
				}
				else
				{
					intialHash = hashFunction(intialHash, pointer.Read<byte>());
					size -= sizeof(byte);
				}
			return intialHash;
		}

		/// <summary>
		/// Computes hash code for the structure content.
		/// </summary>
		/// <typeparam name="T">Stucture type.</typeparam>
		/// <param name="value"></param>
		/// <returns>Content hash code.</returns>
		public static int BitwiseHashCode<T>(this T value)
			where T : struct
			=> BitwiseHashCode(value, 1474027755, (hash, word) => hash * -1521134295 + word);

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
			=> new ReadOnlySpan<byte>(Unsafe.AsPointer(ref first), Unsafe.SizeOf<T1>())
				.SequenceEqual(new ReadOnlySpan<byte>(Unsafe.AsPointer(ref second), Unsafe.SizeOf<T2>()));

		public static unsafe int BitwiseCompare<T1, T2>(this T1 first, T2 second)
			where T1: unmanaged
			where T2: unmanaged
			=> new ReadOnlySpan<byte>(Unsafe.AsPointer(ref first), Unsafe.SizeOf<T1>())
				.SequenceCompareTo(new ReadOnlySpan<byte>(Unsafe.AsPointer(ref second), Unsafe.SizeOf<T2>()));

		/// <summary>
		/// Converts one structure into another without changing any bits.
		/// </summary>
		/// <param name="input"></param>
		/// <typeparam name="I"></typeparam>
		/// <typeparam name="O"></typeparam>
		/// <returns></returns>
		public static unsafe O BitCast<I, O>(this I input)
			where I : struct
			where O : unmanaged
		{
			var output = new O();
			var size = (uint)Math.Min(Unsafe.SizeOf<I>(), Unsafe.SizeOf<O>());
			Unsafe.CopyBlockUnaligned(Unsafe.AsPointer(ref output), Unsafe.AsPointer(ref input), size);
			return output;
		}

		/// <summary>
		/// Convert binary representation of structure into array of bytes.
		/// </summary>
		/// <param name="input">A structure to convert.</param>
		/// <typeparam name="T">Structure type.</typeparam>
		/// <returns>An array containing binary content of the structure in the form of bytes.</returns>
		public unsafe static byte[] AsBinary<T>(this T input)
			where T: struct
			=> new ReadOnlySpan<byte>(Unsafe.AsPointer(ref input), Unsafe.SizeOf<T>()).ToArray();
		
		public static bool IsDefault<T>(this T value)
			where T: struct
			=> BitwiseEquals(value, default(T));

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
		public static Box<T> Box<T>(this T value)
			where T: struct
			=> new Box<T>(value);
    }
}