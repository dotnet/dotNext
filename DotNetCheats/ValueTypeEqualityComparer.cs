using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace Cheats.Reflection
{
	/// <summary>
	/// Represents generic equality comparer for any value type.
	/// </summary>
	/// <remarks>
	/// Implementation of this class based on bitwise representation
	/// of the value type.
	/// </remarks>
	/// <typeparam name="T">Value type.</typeparam>
    public sealed class ValueTypeEqualityComparer<T>: IEqualityComparer<T>
        where T: struct
    {
        private ValueTypeEqualityComparer()
        {
        }

		/// <summary>
		/// Gets singleton instance of value type equality comparer.
		/// </summary>
        public static IEqualityComparer<T> Instance { get; } = typeof(T).IsPrimitive ? EqualityComparer<T>.Default.Upcast<IEqualityComparer<T>, EqualityComparer<T>>() : new ValueTypeEqualityComparer<T>();
		
		/// <summary>
		/// Performs bitwise equality between two value types.
		/// </summary>
		/// <param name="first">The first value type to compare.</param>
		/// <param name="second">The second value type to compare.</param>
		/// <returns>True, of both value types are bitwise equal; otherwise, false.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(T first, T second) => first.BitwiseEquals(second);

		/// <summary>
		/// Performs bitwise computation of hash code.
		/// </summary>
		/// <param name="obj">Value type to be hashed.</param>
		/// <returns>Bitwise hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(T obj) => obj.BitwiseHashCode();
    }
}