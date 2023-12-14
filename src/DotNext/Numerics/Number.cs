using System.Numerics;

namespace DotNext.Numerics;

/// <summary>
/// Represents Generic Math extensions.
/// </summary>
public static class Number
{
    /// <summary>
    /// Determines whether the specified numeric type is signed.
    /// </summary>
    /// <typeparam name="T">The type to check.</typeparam>
    /// <returns>
    /// <see langword="true"/> if <typeparamref name="T"/> is a signed numeric type;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsSigned<T>()
        where T : notnull, INumberBase<T>
        => T.IsNegative(-T.One);

    /// <summary>
    /// Gets maximum number of bytes that can be used by <typeparamref name="T"/> type
    /// when encoded in little-endian or big-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type to check.</typeparam>
    /// <returns>The maximum numbers bytes that can be occupied by the value of <typeparamref name="T"/>.</returns>
    public static int GetMaxByteCount<T>()
        where T : notnull, IBinaryInteger<T>
        => T.AllBitsSet.GetByteCount();
}