using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DotNext
{
    using Runtime.InteropServices;

    /// <summary>
    /// Provides fast memory operations to work with value type.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    public static class ValueType<T>
        where T: struct
    {
        private sealed class BitwiseComparer: IEqualityComparer<T>, IComparer<T>
        {
            internal static readonly BitwiseComparer Instance = new BitwiseComparer();
            private BitwiseComparer()
            {
            }

            bool IEqualityComparer<T>.Equals(T first, T second) => ValueType<T>.Equals(first, second);

            int IEqualityComparer<T>.GetHashCode(T obj) => ValueType<T>.GetHashCode(obj);
            int IComparer<T>.Compare(T first, T second) => ValueType<T>.Compare(first, second);
        }

        /// <summary>
        /// Size of value type, in bytes.
        /// </summary>
        public static readonly int Size = Unsafe.SizeOf<T>();

        /// <summary>
        /// Default value of type <typeparamref name="T"/>.
        /// </summary>
        public static T Default => default;

		/// <summary>
		/// Indicates that value type is primitive type.
		/// </summary>
        public static readonly bool IsPrimitive = typeof(T).IsPrimitive;

        /// <summary>
        /// Equality comparer for the value type based on its bitwise representation.
        /// </summary>
        public static IEqualityComparer<T> EqualityComparer
        {
            get
            {
                if(IsPrimitive)
                    return EqualityComparer<T>.Default;
                else
                    return BitwiseComparer.Instance;
            }
        }

        /// <summary>
        /// Value comparer for the value type based on its bitwise representation.
        /// </summary>
        public static IComparer<T> Comparer
        {
            get
            {
                if(IsPrimitive)
                    return Comparer<T>.Default;
                else
                    return BitwiseComparer.Instance;
            }
        }

		/// <summary>
		/// Checks bitwise equality between two values of different value types.
		/// </summary>
		/// <remarks>
		/// This method doesn't use <see cref="object.Equals(object)"/>
		/// even if it is overridden by value type.
		/// </remarks>
		/// <typeparam name="U">Type of second value.</typeparam>
		/// <param name="first">The first value to check.</param>
		/// <param name="second">The second value to check.</param>
		/// <returns><see langword="true"/>, if both values are equal; otherwise, <see langword="false"/>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool Equals<U>(T first, U second)
			where U: struct
            => Size == ValueType<U>.Size && Memory.Equals(Unsafe.AsPointer(ref first), Unsafe.AsPointer(ref second), Size);

		/// <summary>
		/// Checks bitwise equality between two values of the same value type.
		/// </summary>
		/// <remarks>
		/// This method doesn't use <see cref="object.Equals(object)"/>
		/// even if it is overridden by value type.
		/// </remarks>
		/// <param name="first">The first value to check.</param>
		/// <param name="second">The second value to check.</param>
		/// <returns><see langword="true"/>, if both values are equal; otherwise, <see langword="false"/>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool Equals(T first, T second)
            => Memory.Equals(Unsafe.AsPointer(ref first), Unsafe.AsPointer(ref second), Size);

		/// <summary>
		/// Computes bitwise hash code for the specified value.
		/// </summary>
		/// <remarks>
		/// This method doesn't use <see cref="object.GetHashCode"/>
		/// even if it is overridden by value type.
		/// </remarks>
		/// <param name="value">A value to be hashed.</param>
		/// <param name="hash">Initial value of the hash.</param>
		/// <param name="hashFunction">Hashing function.</param>
		/// <param name="salted">True to include randomized salt data into hashing; false to use data from memory only.</param>
		/// <returns>Bitwise hash code.</returns>
		public static unsafe int GetHashCode(T value, int hash, Func<int, int, int> hashFunction, bool salted = true)
			=> Memory.GetHashCode(Unsafe.AsPointer(ref value), Size, hash, hashFunction, salted);

		/// <summary>
		/// Computes hash code for the structure content.
		/// </summary>
		/// <param name="value">Value to be hashed.</param>
		/// <param name="salted">True to include randomized salt data into hashing; false to use data from memory only.</param>
		/// <returns>Content hash code.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]      
		public static unsafe int GetHashCode(T value, bool salted = true)
            => Memory.GetHashCode(Unsafe.AsPointer(ref value), Size, salted);
        
        internal unsafe static ReadOnlySpan<byte> RawBits(ref T value)
            => new ReadOnlySpan<byte>(Unsafe.AsPointer(ref value), Size);

		/// <summary>
		/// Indicates that specified value type is the default value.
		/// </summary>
		/// <param name="value">Value to check.</param>
		/// <returns><see langword="true"/>, if value is default value; otherwise, <see langword="false"/>.</returns>
		public static bool IsDefault(T value)
            => Equals(value, default);

		/// <summary>
		/// Convert value type content into array of bytes.
		/// </summary>
		/// <param name="value">A value to convert.</param>
		/// <returns>An array of bytes representing binary content of value type.</returns>
        public static byte[] AsBinary(T value)
            => RawBits(ref value).ToArray();

		/// <summary>
		/// Compares bits of two values of the same type.
		/// </summary>
		/// <param name="first">The first value to compare.</param>
		/// <param name="second">The second value to compare.</param>
		/// <returns>A value that indicates the relative order of the objects being compared.</returns>
		public static unsafe int Compare(T first, T second)
            => Memory.Compare(Unsafe.AsPointer(ref first), Unsafe.AsPointer(ref second), Size);

		/// <summary>
		/// Compares bits of two values of the different type.
		/// </summary>
		/// <typeparam name="U">Type of the second value.</typeparam>
		/// <param name="first">The first value to compare.</param>
		/// <param name="second">The second value to compare.</param>
		/// <returns>A value that indicates the relative order of the objects being compared.</returns>
		public static unsafe int Compare<U>(T first, U second)
            where U: struct
            => Size == ValueType<U>.Size ? 
					Memory.Compare(Unsafe.AsPointer(ref first), Unsafe.AsPointer(ref second), Size) :
					Size.CompareTo(ValueType<U>.Size);
    }
}