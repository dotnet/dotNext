using System.Text;

namespace DotNext.Text;

using Buffers;

/// <summary>
/// Represents extension method for <see cref="Encoding"/> data type.
/// </summary>
public static class EncodingExtensions
{
    private static readonly UTF8Encoding Utf8WithoutPreamble = new(false);

    /// <summary>
    /// Returns <see cref="Encoding"/> that doesn't generate BOM.
    /// </summary>
    /// <param name="encoding">The source encoding.</param>
    /// <returns>The source encoding without BOM.</returns>
    public static Encoding WithoutPreamble(this Encoding encoding)
        => encoding is UTF8Encoding ? Utf8WithoutPreamble : EncodingWithoutPreamble.Create(encoding);

    /// <summary>
    /// Encodes a set of characters from the specified read-only span.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for releasing returned memory.
    /// </remarks>
    /// <param name="encoding">The target encoding.</param>
    /// <param name="chars">The characters to encode.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <returns>The memory containing encoded characters.</returns>
    public static MemoryOwner<byte> GetBytes(this Encoding encoding, ReadOnlySpan<char> chars, MemoryAllocator<byte>? allocator = null)
    {
        MemoryOwner<byte> owner;
        if (chars.IsEmpty)
        {
            owner = default;
        }
        else
        {
            owner = allocator.Invoke(encoding.GetByteCount(chars), true);
            encoding.GetBytes(chars, owner.Memory.Span);
        }

        return owner;
    }

    /// <summary>
    /// Decodes all the bytes in the specified read-only byte.
    /// </summary>
    /// <param name="encoding">The target encoding.</param>
    /// <param name="bytes">The set of bytes representing encoded characters.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <returns>The memory containing decoded characters.</returns>
    public static MemoryOwner<char> GetChars(this Encoding encoding, ReadOnlySpan<byte> bytes, MemoryAllocator<char>? allocator = null)
    {
        MemoryOwner<char> owner;
        if (bytes.IsEmpty)
        {
            owner = default;
        }
        else
        {
            owner = allocator.Invoke(encoding.GetCharCount(bytes), true);
            encoding.GetChars(bytes, owner.Memory.Span);
        }

        return owner;
    }
}