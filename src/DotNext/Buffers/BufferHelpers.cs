using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace DotNext.Buffers;

/// <summary>
/// Represents helper methods to work with various buffer representations.
/// </summary>
public static partial class BufferHelpers
{
    /// <summary>
    /// Converts the sequence of memory blocks to <see cref="ReadOnlySequence{T}"/> data type.
    /// </summary>
    /// <param name="chunks">The sequence of memory blocks.</param>
    /// <typeparam name="T">The type of elements in the memory blocks.</typeparam>
    /// <returns>The constructed <see cref="ReadOnlySequence{T}"/> instance containing memory blocks.</returns>
    public static ReadOnlySequence<T> ToReadOnlySequence<T>(this IEnumerable<ReadOnlyMemory<T>> chunks)
    {
        Chunk<T>? head = null, tail = null;
        foreach (var segment in chunks)
        {
            if (!segment.IsEmpty)
                Chunk<T>.AddChunk(segment, ref head, ref tail);
        }

        if (head is null || tail is null)
            return ReadOnlySequence<T>.Empty;

        if (ReferenceEquals(head, tail))
            return new ReadOnlySequence<T>(head.Memory);

        return Chunk<T>.CreateSequence(head, tail);
    }

    /// <summary>
    /// Constructs a sequence of characters from a collection of strings.
    /// </summary>
    /// <param name="strings">A collection of strings.</param>
    /// <returns>A sequence of characters representing concatenated strings.</returns>
    public static ReadOnlySequence<char> ToReadOnlySequence(this IEnumerable<string?> strings)
    {
        Chunk<char>? head = null, tail = null;

        switch (strings)
        {
            case List<string?> list:
                ToReadOnlySequence(CollectionsMarshal.AsSpan(list), ref head, ref tail);
                break;
            case string?[] array:
                ToReadOnlySequence(array.AsSpan(), ref head, ref tail);
                break;
            default:
                foreach (var str in strings)
                {
                    if (str is { Length: > 0 })
                        Chunk<char>.AddChunk(str.AsMemory(), ref head, ref tail);
                }

                break;
        }

        if (head is null || tail is null)
            return ReadOnlySequence<char>.Empty;

        if (ReferenceEquals(head, tail))
            return new ReadOnlySequence<char>(head.Memory);

        return Chunk<char>.CreateSequence(head, tail);

        static void ToReadOnlySequence(ReadOnlySpan<string?> strings, ref Chunk<char>? head, ref Chunk<char>? tail)
        {
            foreach (var str in strings)
            {
                if (str is { Length: > 0 })
                    Chunk<char>.AddChunk(str.AsMemory(), ref head, ref tail);
            }
        }
    }

    /// <summary>
    /// Converts two memory blocks to <see cref="ReadOnlySequence{T}"/> data type.
    /// </summary>
    /// <param name="first">The first memory block.</param>
    /// <param name="second">The second memory block.</param>
    /// <typeparam name="T">The type of elements in the memory blocks.</typeparam>
    /// <returns>The constructed <see cref="ReadOnlySequence{T}"/> instance containing memory blocks.</returns>
    public static ReadOnlySequence<T> Concat<T>(this ReadOnlyMemory<T> first, ReadOnlyMemory<T> second)
    {
        if (first.IsEmpty)
            return second.IsEmpty ? ReadOnlySequence<T>.Empty : new ReadOnlySequence<T>(second);

        if (second.IsEmpty)
            return new ReadOnlySequence<T>(first);

        Chunk<T>? head = null, tail = null;
        Chunk<T>.AddChunk(first, ref head, ref tail);
        Chunk<T>.AddChunk(second, ref head, ref tail);
        return Chunk<T>.CreateSequence(head, tail);
    }

    /// <summary>
    /// Constructs the string from the buffer.
    /// </summary>
    /// <param name="writer">The buffer of characters.</param>
    /// <returns>The string constructed from the buffer.</returns>
    public static string BuildString(this ArrayBufferWriter<char> writer)
    {
        var span = writer.WrittenSpan;
        return span.IsEmpty ? string.Empty : new string(span);
    }

    /// <summary>
    /// Writes single element to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to add.</param>
    /// <typeparam name="T">The type of elements in the buffer.</typeparam>
    public static void Write<T>(this IBufferWriter<T> writer, T value)
    {
        const int count = 1;
        writer.GetSpan()[0] = value;
        writer.Advance(count);
    }

    /// <summary>
    /// Writes the sequence of elements to the buffer.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The sequence of elements to be written.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T>(this IBufferWriter<T> writer, in ReadOnlySequence<T> value)
    {
        if (value.IsSingleSegment)
        {
            writer.Write(value.FirstSpan);
        }
        else
        {
            WriteSlow(writer, in value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void WriteSlow(IBufferWriter<T> writer, in ReadOnlySequence<T> value)
        {
            foreach (var segment in value)
                writer.Write(segment.Span);
        }
    }

    /// <summary>
    /// Copies the contents from the source sequence into a destination span.
    /// </summary>
    /// <param name="source">Source sequence.</param>
    /// <param name="destination">Destination memory.</param>
    /// <param name="writtenCount">The number of copied elements.</param>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyTo<T>(this in ReadOnlySequence<T> source, Span<T> destination, out int writtenCount)
    {
        if (source.IsSingleSegment)
        {
            // fast path - single-segment sequence
            source.FirstSpan.CopyTo(destination, out writtenCount);
        }
        else
        {
            // slow path - multisegment sequence
            writtenCount = CopyToSlow(in source, destination);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int CopyToSlow(in ReadOnlySequence<T> source, Span<T> destination)
        {
            int result = 0, subcount;

            for (var position = source.Start; !destination.IsEmpty && source.TryGet(ref position, out var block); result += subcount)
            {
                block.Span.CopyTo(destination, out subcount);
                destination = destination.Slice(subcount);
            }

            return result;
        }
    }

    /// <summary>
    /// Releases all resources encapsulated by the container.
    /// </summary>
    /// <remarks>
    /// This method calls <see cref="IDisposable.Dispose"/> for each
    /// object in the rented block.
    /// </remarks>
    /// <typeparam name="T">The type of items in the rented memory.</typeparam>
    /// <param name="owner">The rented memory.</param>
    public static void ReleaseAll<T>(this ref MemoryOwner<T> owner)
        where T : notnull, IDisposable
    {
        foreach (ref var item in owner.Span)
        {
            item.Dispose();
            item = default!;
        }

        owner.Clear(false);
    }

    /// <summary>
    /// Trims the memory block to specified length if it exceeds it.
    /// If length is less that <paramref name="maxLength" /> then the original block returned.
    /// </summary>
    /// <typeparam name="T">The type of items in the span.</typeparam>
    /// <param name="memory">A contiguous region of arbitrary memory.</param>
    /// <param name="maxLength">Maximum length.</param>
    /// <returns>Trimmed memory block.</returns>
    public static ReadOnlyMemory<T> TrimLength<T>(this ReadOnlyMemory<T> memory, int maxLength)
        => memory.Length <= maxLength ? memory : memory.Slice(0, maxLength);

    /// <summary>
    /// Trims the memory block to specified length if it exceeds it.
    /// If length is less that <paramref name="maxLength" /> then the original block returned.
    /// </summary>
    /// <typeparam name="T">The type of items in the span.</typeparam>
    /// <param name="memory">A contiguous region of arbitrary memory.</param>
    /// <param name="maxLength">Maximum length.</param>
    /// <returns>Trimmed memory block.</returns>
    public static Memory<T> TrimLength<T>(this Memory<T> memory, int maxLength)
        => memory.Length <= maxLength ? memory : memory.Slice(0, maxLength);

    /// <summary>
    /// Writes the contents of string builder to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="input">The string builder.</param>
    public static void Write(this IBufferWriter<char> writer, StringBuilder input)
    {
        foreach (var chunk in input.GetChunks())
            writer.Write(chunk.Span);
    }

    /// <summary>
    /// Writes interpolated string to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="provider">The formatting provider.</param>
    /// <param name="handler">The handler of the interpolated string.</param>
    /// <returns>The number of written characters.</returns>
    public static int WriteString(this IBufferWriter<char> writer, IFormatProvider? provider, [InterpolatedStringHandlerArgument("writer", "provider")] ref BufferWriterInterpolatedStringHandler handler)
        => handler.WrittenCount;

    /// <summary>
    /// Writes interpolated string to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="handler">The handler of the interpolated string.</param>
    /// <returns>The number of written characters.</returns>
    public static int WriteString(this IBufferWriter<char> writer, [InterpolatedStringHandlerArgument("writer")] ref BufferWriterInterpolatedStringHandler handler)
        => WriteString(writer, null, ref handler);

    /// <summary>
    /// Writes the value as a string.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>The number of written characters.</returns>
    public static int WriteAsString<T>(this IBufferWriter<char> writer, T value, string? format = null, IFormatProvider? provider = null)
        => BufferWriterInterpolatedStringHandler.AppendFormatted(writer, value, format, provider);

    /// <summary>
    /// Writes line termination symbols to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    public static void WriteLine(this IBufferWriter<char> writer)
        => writer.Write(Environment.NewLine);

    /// <summary>
    /// Writes a string to the buffer, followed by a line terminator.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="characters">The characters to write.</param>
    public static void WriteLine(this IBufferWriter<char> writer, ReadOnlySpan<char> characters)
    {
        writer.Write(characters);
        writer.Write(Environment.NewLine);
    }

    /// <summary>
    /// Converts the value to the characters and write them to the buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to convert.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>The number of written characters.</returns>
    public static int WriteFormattable<T>(this IBufferWriter<char> writer, T value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : notnull, ISpanFormattable
    {
        int charsWritten;
        const int maxBufferSize = int.MaxValue / 2;

        for (int bufferSize = 0; ;)
        {
            var buffer = writer.GetSpan(bufferSize);
            if (value.TryFormat(buffer, out charsWritten, format, provider))
            {
                writer.Advance(charsWritten);
                break;
            }

            bufferSize = bufferSize <= maxBufferSize ? buffer.Length * 2 : throw new InsufficientMemoryException();
        }

        return charsWritten;
    }

    /// <summary>
    /// Writes the value as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to convert.</param>
    [RequiresPreviewFeatures]
    public static void WriteFormattable<T>(this IBufferWriter<byte> writer, T value)
        where T : notnull, IBinaryFormattable<T>
    {
        var output = new SpanWriter<byte>(writer.GetSpan(T.Size));
        value.Format(ref output);
        writer.Advance(output.WrittenCount);
    }

    /// <summary>
    /// Writes a sequence of formattable values.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="values">A sequence of values to convert.</param>
    [RequiresPreviewFeatures]
    public static void WriteFormattable<T>(this IBufferWriter<byte> writer, ReadOnlySpan<T> values)
        where T : notnull, IBinaryFormattable<T>
    {
        if (values.IsEmpty)
            return;

        var output = new SpanWriter<byte>(writer.GetSpan(checked(T.Size * values.Length)));

        foreach (ref readonly var value in values)
            value.Format(ref output);

        writer.Advance(output.WrittenCount);
    }
}