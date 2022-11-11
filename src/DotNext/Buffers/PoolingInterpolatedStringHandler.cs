using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Buffers;

/// <summary>
/// Represents interpolated string builder that utilizes reusable buffer rented from the pool.
/// </summary>
[InterpolatedStringHandler]
[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Auto)]
public struct PoolingInterpolatedStringHandler : IGrowableBuffer<char>, IDisposable
{
    private const int MaxBufferSize = int.MaxValue / 2;
    private const char Whitespace = ' ';

    private readonly MemoryAllocator<char>? allocator;
    private readonly IFormatProvider? provider;
    private MemoryOwner<char> buffer;
    private int count;

    /// <summary>
    /// Initializes a new interpolated string handler.
    /// </summary>
    /// <param name="literalLength">The total number of characters in known at compile-time.</param>
    /// <param name="formattedCount">The number of placeholders.</param>
    /// <param name="allocator">The buffer allocator.</param>
    /// <param name="provider">Optional formatting provider.</param>
    public PoolingInterpolatedStringHandler(int literalLength, int formattedCount, MemoryAllocator<char>? allocator, IFormatProvider? provider = null)
    {
        buffer = allocator.Invoke(literalLength + formattedCount, exactSize: false);
        this.allocator = allocator;
        this.provider = provider;
        count = 0;
    }

    /// <inheritdoc />
    readonly long IGrowableBuffer<char>.WrittenCount => count;

    /// <inheritdoc />
    void IGrowableBuffer<char>.Write(ReadOnlySpan<char> value) => AppendFormatted(value);

    /// <inheritdoc />
    void IReadOnlySpanConsumer<char>.Invoke(scoped ReadOnlySpan<char> value) => AppendFormatted(value);

    /// <inheritdoc />
    void IGrowableBuffer<char>.Write(char value) => AppendFormatted(MemoryMarshal.CreateReadOnlySpan(ref value, 1));

    /// <inheritdoc />
    readonly void IGrowableBuffer<char>.CopyTo<TConsumer>(TConsumer consumer) => consumer.Invoke(WrittenMemory.Span);

    /// <inheritdoc />
    readonly ValueTask IGrowableBuffer<char>.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
        => consumer.Invoke(WrittenMemory, token);

    /// <inheritdoc />
    readonly int IGrowableBuffer<char>.CopyTo(scoped Span<char> output)
    {
        WrittenMemory.Span.CopyTo(output, out var writtenCount);
        return writtenCount;
    }

    /// <inheritdoc />
    void IGrowableBuffer<char>.Clear()
    {
        buffer.Dispose();
        count = 0;
    }

    /// <inheritdoc />
    readonly bool IGrowableBuffer<char>.TryGetWrittenContent(out ReadOnlyMemory<char> block)
    {
        block = WrittenMemory;
        return true;
    }

    private readonly ReadOnlyMemory<char> WrittenMemory => count > 0 ? buffer.Memory.Slice(0, count) : ReadOnlyMemory<char>.Empty;

    internal MemoryOwner<char> DetachBuffer()
    {
        MemoryOwner<char> result;

        if (count is 0)
        {
            result = default;
        }
        else
        {
            result = buffer;
            result.Truncate(count);
            count = 0;
            buffer = default;
        }

        return result;
    }

    private Span<char> GetSpan(int sizeHint)
    {
        if (IGrowableBuffer<char>.GetBufferSize(sizeHint, buffer.Length, count, out sizeHint))
            buffer.Resize(sizeHint, exactSize: false, allocator);

        return buffer.Span.Slice(count);
    }

    /// <summary>
    /// Writes the specified string to the handler.
    /// </summary>
    /// <param name="value">The string to write.</param>
    public void AppendLiteral(string? value)
        => AppendFormatted(value.AsSpan());

    /// <summary>
    /// Writes the specified value to the handler.
    /// </summary>
    /// <typeparam name="T">The type of the value to write.</typeparam>
    /// <param name="value">The value to write.</param>
    /// <param name="format">The format string.</param>
    public void AppendFormatted<T>(T value, string? format = null)
    {
        switch (value)
        {
            case IFormattable:
                if (value is ISpanFormattable)
                {
                    int charsWritten;
                    for (int bufferSize = 0; ; bufferSize = bufferSize <= MaxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException())
                    {
                        var span = GetSpan(bufferSize);

                        // constrained call avoiding boxing for value types
                        if (((ISpanFormattable)value).TryFormat(span, out charsWritten, format, provider))
                            break;
                    }

                    count += charsWritten;
                }
                else
                {
                    // constrained call avoiding boxing for value types
                    AppendLiteral(((IFormattable)value).ToString(format, provider));
                }

                break;
            case not null:
                AppendLiteral(value.ToString());
                break;
        }
    }

    private void AppendFormatted(ReadOnlySpan<char> value, int alignment, bool leftAlign)
    {
        Debug.Assert(alignment >= 0);

        var padding = alignment - value.Length;
        if (padding <= 0)
        {
            AppendFormatted(value);
            return;
        }

        var span = GetSpan(alignment);
        if (leftAlign)
        {
            span.Slice(value.Length, padding).Fill(Whitespace);
            value.CopyTo(span);
        }
        else
        {
            span.Slice(0, padding).Fill(Whitespace);
            value.CopyTo(span.Slice(padding));
        }

        count += alignment;
    }

    /// <summary>
    /// Writes the specified string of chars to the handler.
    /// </summary>
    /// <param name="value">The span to write.</param>
    /// <param name="alignment">
    /// Minimum number of characters that should be written for this value. If the value is negative,
    /// it indicates left-aligned and the required minimum is the absolute value.
    /// </param>
    public void AppendFormatted(ReadOnlySpan<char> value, int alignment)
    {
        var leftAlign = false;

        if (alignment < 0)
        {
            leftAlign = true;
            alignment = -alignment;
        }

        AppendFormatted(value, alignment, leftAlign);
    }

    /// <summary>
    /// Writes the specified value to the handler.
    /// </summary>
    /// <typeparam name="T">The type of the value to write.</typeparam>
    /// <param name="value">The value to write.</param>
    /// <param name="alignment">
    /// Minimum number of characters that should be written for this value. If the value is negative,
    /// it indicates left-aligned and the required minimum is the absolute value.
    /// </param>
    /// <param name="format">The format string.</param>
    public void AppendFormatted<T>(T value, int alignment, string? format = null)
    {
        var leftAlign = false;

        if (alignment < 0)
        {
            leftAlign = true;
            alignment = -alignment;
        }

        switch (value)
        {
            case IFormattable:
                if (value is ISpanFormattable)
                {
                    for (int bufferSize = alignment; ; bufferSize = bufferSize <= MaxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException())
                    {
                        var span = GetSpan(bufferSize);
                        if (((ISpanFormattable)value).TryFormat(span, out var charsWritten, format, provider))
                        {
                            var padding = alignment - charsWritten;

                            if (padding <= 0)
                            {
                                alignment = charsWritten;
                            }
                            else if (leftAlign)
                            {
                                span.Slice(charsWritten, padding).Fill(Whitespace);
                            }
                            else
                            {
                                span.Slice(0, charsWritten).CopyTo(span.Slice(padding));
                                span.Slice(0, padding).Fill(Whitespace);
                            }

                            count += alignment;
                            break;
                        }
                    }
                }
                else
                {
                    AppendFormatted(((IFormattable)value).ToString(format, provider).AsSpan(), alignment, leftAlign);
                }

                break;
            case not null:
                AppendFormatted(value.ToString().AsSpan(), alignment, leftAlign);
                break;
        }
    }

    /// <summary>
    /// Writes the specified character span to the handler.
    /// </summary>
    /// <param name="value">The span to write.</param>
    public void AppendFormatted(ReadOnlySpan<char> value)
    {
        value.CopyTo(GetSpan(value.Length));
        count += value.Length;
    }

    /// <inheritdoc />
    public readonly override string ToString() => WrittenMemory.ToString();

    /// <summary>
    /// Releases the buffer associated with this handler.
    /// </summary>
    public void Dispose()
    {
        buffer.Dispose();
        this = default;
    }
}