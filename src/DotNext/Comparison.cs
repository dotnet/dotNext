using System.Runtime.CompilerServices;

namespace DotNext;

/// <summary>
/// Provides generic methods to work with comparable values.
/// </summary>
public static class Comparison
{
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