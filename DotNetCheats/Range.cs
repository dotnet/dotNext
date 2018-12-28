using System;

namespace DotNetCheats
{
    public static class Range
    {
        /// <summary>
        ///     Restricts a <paramref name="value" /> in specific range.
        /// </summary>
        /// <typeparam name="T">Type of the values.</typeparam>
        /// <param name="value">Value.</param>
        /// <param name="min">Minimal range value.</param>
        /// <param name="max">Maximum range value.</param>
        public static T Clamp<T> (this T value, T min, T max, BoundType boundType = BoundType.Open) 
            where T : IComparable<T>
            => value.Max(min).Min(max);


        /// <summary>
        ///     Restricts a <paramref name="value" /> minimal value.
        /// </summary>
        /// <typeparam name="T">Type of the values.</typeparam>
        /// <param name="value">Value.</param>
        /// <param name="min">Minimal value.</param>
        public static T Min<T> (this T value, T min) where T : IComparable<T>
            => value.CompareTo (min) < 0 ? min : value;


        /// <summary>
        ///     Restricts a <paramref name="value" /> maximum value.
        /// </summary>
        /// <typeparam name="T">Type of the values.</typeparam>
        /// <param name="value">Value.</param>
        /// <param name="max">Maximum value.</param>
        public static T Max<T> (this T value, T max) 
            where T : IComparable<T>
            => value.CompareTo (max) > 0 ? max : value;

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
                    return rightCmp >= leftCmp;
                default:
                    return false;
            }
        }
    }
}