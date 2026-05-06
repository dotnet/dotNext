using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

/// <summary>
/// Represents indirection layer for <see cref="IBufferWriter{T}"/> instance.
/// </summary>
/// <param name="writer">The buffer writer.</param>
/// <typeparam name="T">The type of the elements in the buffer.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly record struct BufferConsumer<T>(IBufferWriter<T> writer) : IReadOnlySpanConsumer<T>
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
    public override string? ToString() => writer?.ToString();
}