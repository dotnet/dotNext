namespace DotNext;

/// <summary>
/// Provides extensions for <see cref="TimeSpan"/> data type.
/// </summary>
public static class TimeSpanExtensions
{
    /// <summary>
    /// Extends <see cref="TimeSpan"/> data type.
    /// </summary>
    extension(TimeSpan)
    {
        /// <summary>
        /// Compares two values to compute which is greater.
        /// </summary>
        /// <param name="x">The first value to compare.</param>
        /// <param name="y">The second value to compare.</param>
        /// <returns><paramref name="x"/> if it's greater than <paramref name="y"/>; otherwise, <paramref name="y"/>.</returns>
        public static TimeSpan Max(TimeSpan x, TimeSpan y)
            => new(long.Max(x.Ticks, y.Ticks));

        /// <summary>
        /// Compares two values to compute which is lesser.
        /// </summary>
        /// <param name="x">The first value to compare.</param>
        /// <param name="y">The second value to compare.</param>
        /// <returns><paramref name="x"/> if it's less than <paramref name="y"/>; otherwise, <paramref name="y"/>.</returns>
        public static TimeSpan Min(TimeSpan x, TimeSpan y)
            => new(long.Min(x.Ticks, y.Ticks));
    }
}