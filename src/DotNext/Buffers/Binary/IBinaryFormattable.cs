namespace DotNext.Buffers.Binary;

/// <summary>
/// Represents an object that can be converted to and restored from the binary representation.
/// </summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
public interface IBinaryFormattable<out TSelf>
    where TSelf : IBinaryFormattable<TSelf>, allows ref struct
{
    /// <summary>
    /// Gets size of the object, in bytes.
    /// </summary>
    public static abstract int Size { get; }

    /// <summary>
    /// Formats object as a sequence of bytes.
    /// </summary>
    /// <param name="destination">The output buffer.</param>
    void Format(scoped Span<byte> destination);

    /// <summary>
    /// Restores the object from its binary representation.
    /// </summary>
    /// <param name="source">The input buffer.</param>
    /// <returns>The restored object.</returns>
    public static abstract TSelf Parse(scoped ReadOnlySpan<byte> source);
}