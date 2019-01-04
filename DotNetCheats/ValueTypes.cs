using System;
using System.Runtime.CompilerServices;
using System.Linq;
using static System.Runtime.CompilerServices.Unsafe;

namespace Cheats
{
	/// <summary>
	/// Various extensions for value types.
	/// </summary>
    public static class ValueTypes
    {
		private static readonly int BitwiseHashSalt = new Random().Next();

		public static unsafe int BitwiseHashCode<T>(this T value, int hash, Func<int, int, int> hashFunction)
			where T : struct
		{
			var pointer = new IntPtr(AsPointer(ref value));
			for (var size = SizeOf<T>(); size > 0;)
				if(size > sizeof(int))
				{
					hash = hashFunction(hash, pointer.Read<int>());
					size -= sizeof(int);
				}
				else
				{
					hash = hashFunction(hash, pointer.Read<byte>());
					size -= sizeof(byte);
				}
			
			return hashFunction(hash, BitwiseHashSalt);
		}

		/// <summary>
		/// Computes hash code for the structure content.
		/// </summary>
		/// <remarks>
		/// This method uses FNV-1a hash algorithm.
		/// </remarks>
		/// <typeparam name="T">Stucture type.</typeparam>
		/// <param name="value"></param>
		/// <returns>Content hash code.</returns>
		/// <seealso cref="http://www.isthe.com/chongo/tech/comp/fnv/#FNV-1a">FNV-1a</seealso>
		public static int BitwiseHashCode<T>(this T value)
			where T : struct
			=> BitwiseHashCode(value, unchecked((int)2166136261), (hash, word) => (hash ^ word) * 16777619);

		/// <summary>
		/// Computes bitwise equality between two value types.
		/// </summary>
		/// <param name="first">The first structure to compare.</param>
		/// <param name="second">The second structure to compare.</param>
		/// <typeparam name="T1"></typeparam>
		/// <typeparam name="T2"></typeparam>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe bool BitwiseEquals<T1, T2>(this T1 first, T2 second)
			where T1: struct
			where T2: struct
			=> new ReadOnlySpan<byte>(AsPointer(ref first), SizeOf<T1>())
				.SequenceEqual(new ReadOnlySpan<byte>(AsPointer(ref second), SizeOf<T2>()));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe int BitwiseCompare<T1, T2>(this T1 first, T2 second)
			where T1: unmanaged
			where T2: unmanaged
			=> new ReadOnlySpan<byte>(AsPointer(ref first), SizeOf<T1>())
				.SequenceCompareTo(new ReadOnlySpan<byte>(AsPointer(ref second), SizeOf<T2>()));

		/// <summary>
		/// Converts one structure into another without changing any bits.
		/// </summary>
		/// <param name="input"></param>
		/// <typeparam name="I"></typeparam>
		/// <typeparam name="O"></typeparam>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe O BitCast<I, O>(this I input)
			where I : struct
			where O : unmanaged
		{
			var output = new O();
			CopyBlockUnaligned(AsPointer(ref output), AsPointer(ref input), (uint)Math.Min(SizeOf<I>(), SizeOf<O>()));
			return output;
		}

		/// <summary>
		/// Convert binary representation of structure into array of bytes.
		/// </summary>
		/// <param name="input">A structure to convert.</param>
		/// <typeparam name="T">Structure type.</typeparam>
		/// <returns>An array containing binary content of the structure in the form of bytes.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe static byte[] AsBinary<T>(this T input)
			where T: struct
			=> new ReadOnlySpan<byte>(AsPointer(ref input), SizeOf<T>()).ToArray();
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Box<T> Box<T>(this T value)
			where T: struct
			=> new Box<T>(value);
    }
}