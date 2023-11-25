using System.Buffers;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers;

/// <summary>
/// Represents memory writer that uses pooled memory.
/// </summary>
/// <typeparam name="T">The data type that can be written.</typeparam>
/// <remarks>
/// Initializes a new writer with the default initial capacity.
/// </remarks>
/// <param name="allocator">The allocator of internal buffer.</param>
public sealed class PooledBufferWriter<T>(MemoryAllocator<T>? allocator = null) : BufferWriter<T>, IMemoryOwner<T>
{
    private readonly MemoryAllocator<T>? allocator = allocator;
    private MemoryOwner<T> buffer;

    /// <inheritdoc />
    public override int Capacity
    {
        get => buffer.Length;

        init
        {
            switch (value)
            {
                case < 0:
                    throw new ArgumentOutOfRangeException(nameof(value));
                case > 0:
                    buffer = allocator.AllocateAtLeast(value);
                    break;
            }
        }
    }

    private Memory<T> GetWrittenMemory()
    {
        ThrowIfDisposed();
        return buffer.Memory.Slice(0, position);
    }

    /// <summary>
    /// Gets the data written to the underlying buffer so far.
    /// </summary>
    /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
    public override ReadOnlyMemory<T> WrittenMemory => GetWrittenMemory();

    /// <inheritdoc />
    Memory<T> IMemoryOwner<T>.Memory => GetWrittenMemory();

    /// <summary>
    /// Clears the data written to the underlying memory.
    /// </summary>
    /// <param name="reuseBuffer"><see langword="true"/> to reuse the internal buffer; <see langword="false"/> to destroy the internal buffer.</param>
    /// <exception cref="ObjectDisposedException">This writer has been disposed.</exception>
    public override void Clear(bool reuseBuffer = false)
    {
        ThrowIfDisposed();

        if (!reuseBuffer)
        {
            buffer.Dispose();
        }
        else if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            buffer.Span.Clear();
        }

        position = 0;
    }

    private ref readonly MemoryOwner<T> GetBuffer(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
        ThrowIfDisposed();

        CheckAndResizeBuffer(sizeHint);
        return ref buffer;
    }

    /// <inheritdoc/>
    public override Memory<T> GetMemory(int sizeHint = 0)
        => GetBuffer(sizeHint).Memory.Slice(position);

    /// <inheritdoc/>
    public override Span<T> GetSpan(int sizeHint = 0)
        => GetBuffer(sizeHint).Span.Slice(position);

    /// <inheritdoc />
    public override MemoryOwner<T> DetachBuffer()
    {
        ThrowIfDisposed();
        MemoryOwner<T> result;
        if (position > 0)
        {
            result = buffer;
            buffer = default;
            result.Truncate(position);
            position = 0;
        }
        else
        {
            result = default;
        }

        return result;
    }

    /// <inheritdoc/>
    private protected override void Resize(int newSize)
    {
        buffer.Resize(newSize, allocator);
        PooledBufferWriter.AllocationMeter.Record(buffer.Length, measurementTags);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        buffer.Dispose();
        base.Dispose(disposing);
    }
}

file static class PooledBufferWriter
{
    internal static readonly Histogram<int> AllocationMeter;

    static PooledBufferWriter()
    {
        var meter = new Meter("DotNext.Buffers.PooledBuffer");
        AllocationMeter = meter.CreateHistogram<int>("capacity", description: "Capacity");
    }
}