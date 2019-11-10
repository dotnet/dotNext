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
        public static T Clamp<T>(this T value, T min, T max)
            where T : notnull, IComparable<T>
            => (Math.Sign(value.CompareTo(min)) + Math.Sign(value.CompareTo(max))) switch
            {
                2 => max,
                -2 => min,
                _ => value
            };

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
            where T : notnull, IComparable<T>
            => (Math.Sign(value.CompareTo(left)) + Math.Sign(value.CompareTo(right))) switch
            {
                0 => true,
                1 => (boundType & BoundType.RightClosed) != 0,
                -1 => (boundType & BoundType.LeftClosed) != 0,
                _ => false,
            };
    }
}