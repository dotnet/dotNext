namespace DotNext.Buffers.Text;

/// <summary>
/// Exposes methods for conversion to/from UTF-8.
/// </summary>
public static class Utf8Formattable
{
    /// <summary>
    /// Formats the specified value as a set of UTF-8 encoded characters.
    /// </summary>
    /// <typeparam name="T">The type convertible to UTF-8.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="format">A standard or custom format string that defines the acceptable format for the result.</param>
    /// <param name="provider">culture-specific formatting information.</param>
    /// <param name="allocator">The allocator of the buffer.</param>
    /// <returns>An array of bytes containing UTF-8 encoded characters.</returns>
    /// <exception cref="InsufficientMemoryException">There is no array of appropriate size to place all UTF-8 encoded characters.</exception>
    public static MemoryOwner<byte> GetBytes<T>(this T value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, MemoryAllocator<byte>? allocator = null)
        where T : notnull, IUtf8SpanFormattable
    {
        const int maxBufferSize = int.MaxValue / 2;

        int bytesWritten;
        var result = allocator.AllocateAtLeast(MemoryRental<byte>.StackallocThreshold);
        for (int sizeHint; !value.TryFormat(result.Span, out bytesWritten, format, provider); result.Resize(sizeHint, allocator))
        {
            sizeHint = result.Length;
            sizeHint = sizeHint <= maxBufferSize ? sizeHint << 1 : throw new InsufficientMemoryException();
        }

        result.Truncate(bytesWritten);
        return result;
    }
}