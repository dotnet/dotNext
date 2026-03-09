namespace DotNext.IO;

using Buffers.Binary;

/// <summary>
/// Describes how the length of the octet string should be encoded in binary form.
/// </summary>
public enum LengthFormat : byte
{
    /// <summary>
    /// Use 32-bit integer value to represent octet string length
    /// using little-endian byte order.
    /// </summary>
    LittleEndian = 0,

    /// <summary>
    /// Use 32-bit integer value to represent octet string length
    /// using big-endian byte order.
    /// </summary>
    BigEndian,

    /// <summary>
    /// Use 7-bit encoded compressed integer value to represent octet string length.
    /// </summary>
    /// <remarks>
    /// This format provides optimized binary size.
    /// </remarks>
    Compressed,
}

/// <summary>
/// Provides extensions for <see cref="LengthFormat"/> type.
/// </summary>
public static class LengthFormatExtensions
{
    /// <summary>
    /// Extends <see cref="LengthFormat"/> type.
    /// </summary>
    /// <param name="format">The value to extend.</param>
    extension(LengthFormat format)
    {
        /// <summary>
        /// Gets the maximum amount of bytes needed to represent the length of the specified type.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The format is invalid.</exception>
        public int MaxByteCount => format switch
        {
            LengthFormat.LittleEndian or LengthFormat.BigEndian => sizeof(int),
            LengthFormat.Compressed => Leb128<int>.MaxSizeInBytes,
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        internal bool HasFixedSize => format is LengthFormat.LittleEndian or LengthFormat.BigEndian;
    }
}