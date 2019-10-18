using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DotNext
{
    /// <summary>
    /// Provides generic methods to work with comparable values.
    /// </summary>
    public static class Comparable
    {
        /// <summary>
        /// Restricts a <paramref name="value" /> in specific range.
        /// </summary>
        /// <typeparam name="T">Type of the values.</typeparam>
        /// <param name="value">Value to be restricted.</param>
        /// <param name="min">Minimal range value.</param>
        /// <param name="max">Maximum range value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Clamp<T>(this T value, T min, T max) where T : IComparable<T> => value.Min(max).Max(min);

        /// <summary>
		/// Checks whether specified value is in range.
		/// </summary>
		/// <typeparam name="T">Type of value to check.</typeparam>
		/// <param name="value">Value to check.</param>
		/// <param name="left">Range left bound.</param>
		/// <param name="right">Range right bound.</param>
		/// <param name="boundType">Range endpoints bound type.</param>
		/// <returns><see langword="true"/>, if <paramref name="value"/> is in its bounds.</returns>
        public static bool Between<T>(this T value, T left, T right, BoundType boundType = BoundType.Open)
            where T : IComparable<T>
        {
            switch (Math.Sign(value.CompareTo(left)) + Math.Sign(value.CompareTo(right)))
            {
                case 0:
                    return true;
                case 1:
                    return (boundType & BoundType.RightClosed) != 0;
                case -1:
                    return (boundType & BoundType.LeftClosed) != 0;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns the smaller of two values.
        /// </summary>
        /// <typeparam name="T">Type of the values.</typeparam>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <param name="comparer">Comparison function.</param>
        /// <returns>The smaller of two values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Min<T>(T first, T second, Comparison<T> comparer) => comparer(first, second) < 0 ? first : second;

        /// <summary>
        /// Returns the smaller of two values.
        /// </summary>
        /// <typeparam name="T">Type of the values.</typeparam>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <param name="comparer">Comparison function.</param>
        /// <returns>The smaller of two values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Min<T>(T first, T second, IComparer<T> comparer) => comparer.Compare(first, second) < 0 ? first : second;

        /// <summary>
        /// Returns the smaller of two values.
        /// </summary>
        /// <typeparam name="T">Type of the values.</typeparam>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>The smaller of two values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Min<T>(this T first, T second) where T : IComparable<T> => first.CompareTo(second) < 0 ? first : second;

        /// <summary>
        /// Returns the larger of two values.
        /// </summary>
        /// <typeparam name="T">Type of the values.</typeparam>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <param name="comparer">Comparison function.</param>
        /// <returns>The larger of two values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Max<T>(T first, T second, Comparison<T> comparer)
            => comparer(first, second) > 0 ? first : second;

        /// <summary>
        /// Returns the larger of two values.
        /// </summary>
        /// <typeparam name="T">Type of the values.</typeparam>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <param name="comparer">Comparison function.</param>
        /// <returns>The larger of two values.</returns>       
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Max<T>(T first, T second, IComparer<T> comparer)
            => comparer.Compare(first, second) > 0 ? first : second;

        /// <summary>
		/// Returns the larger of two values.
		/// </summary>
		/// <typeparam name="T">Type of the values.</typeparam>
		/// <param name="first">The first value.</param>
		/// <param name="second">The second value.</param>
        /// <returns>The larger of two values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Max<T>(this T first, T second) where T : IComparable<T> => first.CompareTo(second) > 0 ? first : second;
    }
}