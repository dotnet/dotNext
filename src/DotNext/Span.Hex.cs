namespace DotNext;

using Hex = Buffers.Text.Hex;

public static partial class Span
{
    /// <summary>
    /// Converts set of bytes into hexadecimal representation.
    /// </summary>
    /// <param name="bytes">The bytes to convert.</param>
    /// <param name="output">The buffer used to write hexadecimal representation of bytes.</param>
    /// <param name="lowercased"><see langword="true"/> to return lowercased hex string; <see langword="false"/> to return uppercased hex string.</param>
    /// <returns>The actual number of characters in <paramref name="output"/> written by the method.</returns>
    [Obsolete("Use DotNext.Buffers.Text.Hex.EncodeToUtf16 method instead.")]
    public static int ToHex(this ReadOnlySpan<byte> bytes, Span<char> output, bool lowercased = false)
        => Hex.EncodeToUtf16(bytes, output, lowercased);

    /// <summary>
    /// Converts set of bytes into hexadecimal representation.
    /// </summary>
    /// <param name="bytes">The bytes to convert.</param>
    /// <param name="lowercased"><see langword="true"/> to return lowercased hex string; <see langword="false"/> to return uppercased hex string.</param>
    /// <returns>The hexadecimal representation of bytes.</returns>
    [Obsolete("Use DotNext.Buffers.Text.Hex.EncodeToUtf16 method instead.")]
    public static string ToHex(this ReadOnlySpan<byte> bytes, bool lowercased = false)
        => Hex.EncodeToUtf16(bytes, lowercased);

    /// <summary>
    /// Decodes hexadecimal representation of bytes.
    /// </summary>
    /// <param name="chars">The hexadecimal representation of bytes.</param>
    /// <param name="output">The output buffer used to write decoded bytes.</param>
    /// <returns>The actual number of bytes in <paramref name="output"/> written by the method.</returns>
    /// <exception cref="FormatException"><paramref name="chars"/> contain invalid hexadecimal symbol.</exception>
    [Obsolete("Use DotNext.Buffers.Text.Hex.DecodeFromUtf16 method instead.")]
    public static int FromHex(this ReadOnlySpan<char> chars, Span<byte> output)
        => Hex.DecodeFromUtf16(chars, output);

    /// <summary>
    /// Decodes hexadecimal representation of bytes.
    /// </summary>
    /// <param name="chars">The characters containing hexadecimal representation of bytes.</param>
    /// <returns>The decoded array of bytes.</returns>
    /// <exception cref="FormatException"><paramref name="chars"/> contain invalid hexadecimal symbol.</exception>
    [Obsolete("Use Convert.FromHexString() method instead")]
    public static byte[] FromHex(this ReadOnlySpan<char> chars) => Convert.FromHexString(chars);
}