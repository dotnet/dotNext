using System;
using System.Collections.Generic;

namespace DotNext
{
    /// <summary>
    /// Provides generic methods to work with comparable values.
    /// </summary>
    public static class Comparable
    {
        /// <summary>
        /// Returns the smaller of two values.
        /// </summary>
        /// <typeparam name="T">Type of the values.</typeparam>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <param name="comparer">Comparison function.</param>
        /// <returns>The smaller of two values.</returns>
        public static T Min<T>(T first, T second, Comparison<T> comparer)
            => comparer(first, second) < 0 ? first : second;
        
        /// <summary>
        /// Returns the smaller of two values.
        /// </summary>
        /// <typeparam name="T">Type of the values.</typeparam>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <param name="comparer">Comparison function.</param>
        /// <returns>The smaller of two values.</returns>
        public static T Min<T>(T first, T second, IComparer<T> comparer)
            => Min(first, second, comparer.Compare);
        
        /// <summary>
        /// Returns the smaller of two values.
        /// </summary>
        /// <typeparam name="T">Type of the values.</typeparam>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>The smaller of two values.</returns>
        public static T Min<T>(this T first, T second) where T: IComparable<T> => first.CompareTo(second) < 0 ? first : second;
        
        /// <summary>
		/// Returns the larger of two values.
		/// </summary>
		/// <typeparam name="T">Type of the values.</typeparam>
		/// <param name="first">The first value.</param>
		/// <param name="second">The second value.</param>
        /// <param name="comparer">Comparison function.</param>
        /// <returns>The larger of two values.</returns>       
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
		public static T Max<T>(T first, T second, IComparer<T> comparer)
            => Max(first, second, comparer.Compare);

        /// <summary>
		/// Returns the larger of two values.
		/// </summary>
		/// <typeparam name="T">Type of the values.</typeparam>
		/// <param name="first">The first value.</param>
		/// <param name="second">The second value.</param>
        /// <returns>The larger of two values.</returns>
        public static T Max<T>(this T first, T second) where T: IComparable<T> => first.CompareTo(second) > 0 ? first : second;
    }
}