using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

/// <summary>
/// Represents indirection layer for <see cref="IBufferWriter{T}"/> instance.
/// </summary>
/// <param name="writer">The buffer writer.</param>
/// <typeparam name="T">The type of the elements in the buffer.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly record struct BufferWriterReference<T>(IBufferWriter<T> writer) :
    IBufferWriter<T>,
    IReadOnlySpanConsumer<T>
{
    private readonly IBufferWriter<T> writer = writer;

    /// <summary>
    /// Gets a value indicating that the underlying buffer is <see langword="null"/>.
    /// </summary>
    public bool IsEmpty => writer is null;
    
    /// <inheritdoc />
    void IConsumer<ReadOnlySpan<T>>.Invoke(ReadOnlySpan<T> span) => writer.Write(span);

    /// <inheritdoc />
    ValueTask ISupplier<ReadOnlyMemory<T>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<T> input, CancellationToken token)
        => Invoke(input, token);
    
    private ValueTask Invoke(ReadOnlyMemory<T> input, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = new();
            try
            {
                writer.Write(input.Span);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    void IBufferWriter<T>.Advance(int count) => writer.Advance(count);

    /// <inheritdoc/>
    Memory<T> IBufferWriter<T>.GetMemory(int sizeHint) => writer.GetMemory(sizeHint);

    /// <inheritdoc/>
    Span<T> IBufferWriter<T>.GetSpan(int sizeHint) => writer.GetSpan(sizeHint);

    /// <inheritdoc/>
    public override string? ToString() => writer?.ToString();
}