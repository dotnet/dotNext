using System;

namespace DotNext
{
	/// <summary>
	/// Range checks.
	/// </summary>
    public static class Range
    {
        /// <summary>
        /// Restricts a <paramref name="value" /> in specific range.
        /// </summary>
        /// <typeparam name="T">Type of the values.</typeparam>
        /// <param name="value">Value.</param>
        /// <param name="min">Minimal range value.</param>
        /// <param name="max">Maximum range value.</param>
        public static T Clamp<T> (this T value, T min, T max) 
            where T : IComparable<T>
            => value.Max(min).Min(max);

        /// <summary>
        /// Restricts a <paramref name="first" /> minimal value.
        /// </summary>
        /// <typeparam name="T">Type of the values.</typeparam>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        public static T Min<T> (this T first, T second) where T : IComparable<T>
            => first.CompareTo (second) < 0 ? first : second;

        public static T Min<T>(T first, T second, Comparison<T> comparer)
            => comparer(first, second) < 0 ? first : second;

		/// <summary>
		/// Restricts a <paramref name="first" /> maximum value.
		/// </summary>
		/// <typeparam name="T">Type of the values.</typeparam>
		/// <param name="first">The first value.</param>
		/// <param name="second">The second value.</param>
		public static T Max<T>(this T first, T second)
			where T : IComparable<T>
			=> first.CompareTo(second) > 0 ? first : second;

        public static T Max<T>(T first, T second, Comparison<T> comparer)
            => comparer(first, second) > 0 ? first : second;

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
            where T: IComparable<T>
        {
            int leftCmp = value.CompareTo(left), rightCmp = value.CompareTo(right);
            switch(boundType)
            {
                case BoundType.Open:
                    return leftCmp > 0 && rightCmp < 0;
                case BoundType.LeftClosed:
                    return leftCmp >= 0 && rightCmp < 0;
                case BoundType.RightClosed:
                    return leftCmp > 0 && rightCmp <= 0;
                case BoundType.Closed:
					return leftCmp >= 0 && rightCmp <= 0;
                default:
                    return false;
            }
        }
    }
}