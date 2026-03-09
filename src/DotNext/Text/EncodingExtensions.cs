using System.Text;

namespace DotNext.Text;

using Buffers;

/// <summary>
/// Represents extension method for <see cref="Encoding"/> data type.
/// </summary>
public static class EncodingExtensions
{
    private static readonly UTF8Encoding Utf8WithoutPreamble = new(false);

    /// <param name="encoding">The source encoding.</param>
    extension(Encoding encoding)
    {
        /// <summary>
        /// Returns <see cref="Encoding"/> that doesn't generate BOM.
        /// </summary>
        /// <returns>The source encoding without BOM.</returns>
        public Encoding WithoutPreamble
            => encoding is UTF8Encoding ? Utf8WithoutPreamble : new EncodingWithoutPreamble(encoding);

        /// <summary>
        /// Gets <see cref="Encoding.UTF8"/> encoding that doesn't emit byte order mark.
        /// </summary>
        public static UTF8Encoding UTF8NoBom => Utf8WithoutPreamble;

        /// <summary>
        /// Encodes a set of characters from the specified read-only span.
        /// </summary>
        /// <param name="chars">The characters to encode.</param>
        /// <param name="allocator">The memory allocator.</param>
        /// <returns>The memory containing encoded characters.</returns>
        public MemoryOwner<byte> GetBytes(ReadOnlySpan<char> chars, MemoryAllocator<byte>? allocator = null)
        {
            MemoryOwner<byte> owner;
            if (chars.IsEmpty)
            {
                owner = default;
            }
            else
            {
                owner = allocator.DefaultIfNull.AllocateExactly(encoding.GetByteCount(chars));
                owner.Truncate(encoding.GetBytes(chars, owner.Span));
            }

            return owner;
        }

        /// <summary>
        /// Decodes all the bytes in the specified read-only span.
        /// </summary>
        /// <param name="bytes">The set of bytes representing encoded characters.</param>
        /// <param name="allocator">The memory allocator.</param>
        /// <returns>The memory containing decoded characters.</returns>
        public MemoryOwner<char> GetChars(ReadOnlySpan<byte> bytes, MemoryAllocator<char>? allocator = null)
        {
            MemoryOwner<char> owner;
            if (bytes.IsEmpty)
            {
                owner = default;
            }
            else
            {
                owner = allocator.DefaultIfNull.AllocateExactly(encoding.GetCharCount(bytes));
                owner.Truncate(encoding.GetChars(bytes, owner.Span));
            }

            return owner;
        }
    }
}