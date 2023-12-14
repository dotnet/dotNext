using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers.Binary;

/// <summary>
/// Represents an object that can be converted to and restored from the binary representation.
/// </summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
public interface IBinaryFormattable<TSelf>
    where TSelf : notnull, IBinaryFormattable<TSelf>
{
    /// <summary>
    /// Gets size of the object, in bytes.
    /// </summary>
    public static abstract int Size { get; }

    /// <summary>
    /// Formats object as a sequence of bytes.
    /// </summary>
    /// <param name="output">The output buffer.</param>
    void Format(Span<byte> output);

    /// <summary>
    /// Restores the object from its binary representation.
    /// </summary>
    /// <param name="input">The input buffer.</param>
    /// <returns>The restored object.</returns>
    public static abstract TSelf Parse(ReadOnlySpan<byte> input);

    /// <summary>
    /// Attempts to restore the object from its binary representation.
    /// </summary>
    /// <param name="input">The input buffer.</param>
    /// <param name="result">The restored object.</param>
    /// <returns><see langword="true"/> if the parsing done successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<byte> input, [NotNullWhen(true)] out TSelf? result)
    {
        if (input.Length >= TSelf.Size)
        {
            result = TSelf.Parse(input);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Formats object as a sequence of bytes.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <returns>The buffer containing formatted value.</returns>
    public static MemoryOwner<byte> Format(TSelf value, MemoryAllocator<byte>? allocator = null)
    {
        var result = allocator.AllocateExactly(TSelf.Size);
        value.Format(result.Span);
        return result;
    }

    /// <summary>
    /// Attempts to format object as a sequence of bytes.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="output">The output buffer.</param>
    /// <returns><see langword="true"/> if the value converted successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryFormat(TSelf value, Span<byte> output)
    {
        if (output.Length >= TSelf.Size)
        {
            value.Format(output);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Restores the object from its binary representation.
    /// </summary>
    /// <param name="input">The input buffer.</param>
    /// <returns>The restored object.</returns>
    public static TSelf Parse(in ReadOnlySequence<byte> input)
    {
        var fastBuffer = input.FirstSpan;
        return fastBuffer.Length >= TSelf.Size
            ? TSelf.Parse(fastBuffer)
            : ParseSlow(in input);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static TSelf ParseSlow(in ReadOnlySequence<byte> input)
        {
            using var buffer = (uint)TSelf.Size <= (uint)MemoryRental<byte>.StackallocThreshold
                ? stackalloc byte[TSelf.Size]
                : new MemoryRental<byte>(TSelf.Size);

            input.CopyTo(buffer.Span, out var writtenCount);
            return TSelf.Parse(buffer.Span.Slice(0, writtenCount));
        }
    }
}