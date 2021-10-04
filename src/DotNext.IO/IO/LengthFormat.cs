namespace DotNext.IO;

/// <summary>
/// Describes how the length of the octet string should be encoded in binary form.
/// </summary>
public enum LengthFormat : byte
{
    /// <summary>
    /// Use 32-bit integer value to represent octet string length
    /// using native endianness.
    /// </summary>
    /// <remarks>
    /// This format provides the best performance.
    /// </remarks>
    Plain = 0,

    /// <summary>
    /// Use 32-bit integer value to represent octet string length
    /// using little-endian byte order.
    /// </summary>
    PlainLittleEndian,

    /// <summary>
    /// Use 32-bit integer value to represent octet string length
    /// using big-endian byte order.
    /// </summary>
    PlainBigEndian,

    /// <summary>
    /// Use 7-bit encoded compressed integer value to represent octet string length.
    /// </summary>
    /// <remarks>
    /// This format provides optimized binary size.
    /// </remarks>
    Compressed,
}