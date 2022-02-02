using System.Runtime.CompilerServices;

namespace DotNext;

/// <summary>
/// Provides generic methods to work with comparable values.
/// </summary>
public static class Comparison
{
    /// <summary>
    /// Restricts a <paramref name="value" /> in specific range.
    /// </summary>
    /// <typeparam name="T">Type of the values.</typeparam>
    /// <param name="value">Value to be restricted.</param>
    /// <param name="min">Minimal range value.</param>
    /// <param name="max">Maximum range value.</param>
    /// <returns>
    /// <paramref name="value"/> if <paramref name="min"/> ≤ <paramref name="value"/> ≤ <paramref name="max"/>;
    /// or <paramref name="min"/> if <paramref name="value"/> &lt; <paramref name="min"/>;
    /// or <paramref name="max"/> if <paramref name="max"/> &lt; <paramref name="value"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Clamp<T>(this T value, T min, T max)
        where T : notnull, IComparable<T>
    {
        if (value.CompareTo(min) < 0)
            return min;
        if (value.CompareTo(max) > 0)
            return max;
        return value;
    }

    /// <summary>
    /// Checks whether specified value is in range.
    /// </summary>
    /// <typeparam name="T">Type of value to check.</typeparam>
    /// <param name="value">Value to check.</param>
    /// <param name="left">Range left bound.</param>
    /// <param name="right">Range right bound.</param>
    /// <param name="boundType">Range endpoints bound type.</param>
    /// <returns><see langword="true"/>, if <paramref name="value"/> is in its bounds.</returns>
    public static bool IsBetween<T>(this T value, T left, T right, BoundType boundType = BoundType.Open)
        where T : notnull, IComparable<T>
    {
        int l = value.CompareTo(left), r = value.CompareTo(right);
        return (l > 0 || (l is 0 && (boundType & BoundType.LeftClosed) is not 0))
          && (r < 0 || (r is 0 && (boundType & BoundType.RightClosed) is not 0));
    }
}