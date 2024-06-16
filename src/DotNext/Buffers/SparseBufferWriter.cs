using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.Buffers;

/// <summary>
/// Represents builder of the sparse memory buffer.
/// </summary>
/// <remarks>
/// All members of <see cref="IBufferWriter{T}"/> are explicitly implemented because their
/// usage can produce holes in the sparse buffer. To avoid holes, use public members only.
/// </remarks>
/// <typeparam name="T">The type of the elements in the memory.</typeparam>
/// <seealso cref="PoolingArrayBufferWriter{T}"/>
/// <seealso cref="PoolingBufferWriter{T}"/>
[DebuggerDisplay($"WrittenCount = {{{nameof(WrittenCount)}}}, FragmentedBytes = {{{nameof(FragmentedBytes)}}}")]
public partial class SparseBufferWriter<T> : Disposable, IGrowableBuffer<T>, ISupplier<ReadOnlySequence<T>>
{
    private readonly int chunkSize;
    private readonly MemoryAllocator<T>? allocator;
    private readonly unsafe delegate*<int, ref int, int> growth;
    private int chunkIndex; // used for linear and exponential allocation strategies only
    private MemoryChunk? first;

    [SuppressMessage("Usage", "CA2213", Justification = "Disposed as a part of the linked list")]
    private MemoryChunk? last;
    private long length;

    /// <summary>
    /// Initializes a new builder with the specified size of memory block.
    /// </summary>
    /// <param name="chunkSize">The size of the memory block representing single segment within sequence.</param>
    /// <param name="growth">Specifies how the memory should be allocated for each subsequent chunk in this buffer.</param>
    /// <param name="allocator">The allocator used to rent the segments.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chunkSize"/> is less than or equal to zero.</exception>
    public SparseBufferWriter(int chunkSize, SparseBufferGrowth growth = SparseBufferGrowth.None, MemoryAllocator<T>? allocator = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

        this.chunkSize = chunkSize;
        this.allocator = allocator;

        unsafe
        {
            this.growth = growth switch
            {
                SparseBufferGrowth.Linear => &SparseBufferWriter.LinearGrowth,
                SparseBufferGrowth.Exponential => &SparseBufferWriter.ExponentialGrowth,
                _ => &SparseBufferWriter.NoGrowth,
            };
        }
    }

    /// <summary>
    /// Initializes a new builder with automatically selected
    /// chunk size.
    /// </summary>
    /// <param name="pool">Memory pool used to allocate memory chunks.</param>
    public SparseBufferWriter(MemoryPool<T> pool)
    {
        chunkSize = -1;
        allocator = pool.ToAllocator();
        unsafe
        {
            growth = &SparseBufferWriter.NoGrowth;
        }
    }

    /// <summary>
    /// Initializes a new builder which uses <see cref="MemoryPool{T}.Shared"/>
    /// as a default allocator of buffers.
    /// </summary>
    public SparseBufferWriter()
        : this(MemoryPool<T>.Shared)
    {
    }

    internal MemoryChunk? FirstChunk => first;

    /// <summary>
    /// Gets the number of written elements.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
    public long WrittenCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return length;
        }
    }

    /// <summary>
    /// Gets a value indicating that this buffer consists of a single segment.
    /// </summary>
    public bool IsSingleSegment => ReferenceEquals(first, last);

    /// <summary>
    /// Attempts to get the underlying buffer if it is presented by a single segment.
    /// </summary>
    /// <param name="segment">The single segment representing written content.</param>
    /// <returns><see langword="true"/> if this buffer is represented by a single segment; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
    public bool TryGetWrittenContent(out ReadOnlyMemory<T> segment)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (IsSingleSegment)
        {
            segment = first is null ? ReadOnlyMemory<T>.Empty : first.WrittenMemory;
            return true;
        }

        segment = default;
        return false;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    [ExcludeFromCodeCoverage]
    private long FragmentedBytes
    {
        get
        {
            var result = 0L;
            for (MemoryChunk? current = first, next; current is not null; current = next)
            {
                next = current.Next;
                if (next is { WrittenMemory.Length: > 0 })
                    result += current.FreeCapacity;
            }

            return result;
        }
    }

    /// <summary>
    /// Writes the block of memory to this builder.
    /// </summary>
    /// <param name="input">The memory block to be written to this builder.</param>
    /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
    public unsafe void Write(ReadOnlySpan<T> input)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (last is null)
            first = last = new PooledMemoryChunk(allocator, chunkSize);

        for (int writtenCount; !input.IsEmpty; length += writtenCount)
        {
            writtenCount = last.Write(input);

            // no more space in the last chunk, allocate a new one
            if (writtenCount == 0)
                last = new PooledMemoryChunk(allocator, growth(chunkSize, ref chunkIndex), last);
            else
                input = input.Slice(writtenCount);
        }
    }

    /// <summary>
    /// Writes the block of memory to this builder.
    /// </summary>
    /// <param name="input">The memory block to be written to this builder.</param>
    /// <param name="copyMemory"><see langword="true"/> to copy the content of the input buffer; <see langword="false"/> to import the memory block.</param>
    /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
    public void Write(ReadOnlyMemory<T> input, bool copyMemory = true)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (input.IsEmpty)
            goto exit;

        if (copyMemory)
        {
            Write(input.Span);
        }
        else if (last is null)
        {
            first = last = new ImportedMemoryChunk(input);
            length += input.Length;
        }
        else
        {
            last = new ImportedMemoryChunk(input, last);
            length += input.Length;
        }

    exit:
        return;
    }

    /// <summary>
    /// Adds a single item to the buffer.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
    public void Add(T item) => Write(CreateReadOnlySpan(ref item, 1));

    /// <inheritdoc />
    void IGrowableBuffer<T>.Write(T value) => Add(value);

    /// <summary>
    /// Writes a sequence of memory blocks to this builder.
    /// </summary>
    /// <param name="sequence">A sequence of memory blocks.</param>
    /// <param name="copyMemory"><see langword="true"/> to copy the content of the input buffer; <see langword="false"/> to import memory blocks.</param>
    /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
    public void Write(in ReadOnlySequence<T> sequence, bool copyMemory = true)
    {
        foreach (var segment in sequence)
            Write(segment, copyMemory);
    }

    /// <summary>
    /// Passes the contents of this builder to the consumer.
    /// </summary>
    /// <param name="consumer">The consumer of this buffer.</param>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
    public void CopyTo<TConsumer>(TConsumer consumer)
        where TConsumer : notnull, IReadOnlySpanConsumer<T>
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        for (var current = first; current is not null; current = current.Next)
        {
            consumer.Invoke(current.WrittenMemory.Span);
        }
    }

    /// <inheritdoc />
    async ValueTask IGrowableBuffer<T>.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        for (MemoryChunk? current = first; current is not null; current = current.Next)
        {
            await consumer.Invoke(current.WrittenMemory, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Passes the contents of this builder to the callback.
    /// </summary>
    /// <param name="writer">The callback used to accept memory segments representing the contents of this builder.</param>
    /// <param name="arg">The argument to be passed to the callback.</param>
    /// <typeparam name="TArg">The type of the argument to tbe passed to the callback.</typeparam>
    /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
    public void CopyTo<TArg>(ReadOnlySpanAction<T, TArg> writer, TArg arg)
        => CopyTo(new DelegatingReadOnlySpanConsumer<T, TArg>(writer, arg));

    /// <summary>
    /// Copies the contents of this builder to the specified memory block.
    /// </summary>
    /// <param name="output">The memory block to be modified.</param>
    /// <returns>The actual number of copied elements.</returns>
    /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
    public int CopyTo(Span<T> output)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var total = 0;
        for (MemoryChunk? current = first; current is not null && !output.IsEmpty; current = current.Next)
        {
            var buffer = current.WrittenMemory.Span;
            buffer.CopyTo(output, out var writtenCount);
            output = output.Slice(writtenCount);
            total += writtenCount;
        }

        return total;
    }

    /// <summary>
    /// Clears internal buffers so this builder can be reused.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The builder has been disposed.</exception>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ReleaseChunks();
        length = 0L;
    }

    /// <inheritdoc />
    ReadOnlySequence<T> ISupplier<ReadOnlySequence<T>>.Invoke()
        => TryGetWrittenContent(out var segment) ? new ReadOnlySequence<T>(segment) : Memory.ToReadOnlySequence(this);

    private void ReleaseChunks()
    {
        for (MemoryChunk? current = first, next; current is not null; current = next)
        {
            next = current.Next;
            current.Dispose();
        }

        first = last = null;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ReleaseChunks();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Returns the textual representation of this buffer.
    /// </summary>
    /// <returns>The textual representation of this buffer.</returns>
    public override string ToString()
    {
        return typeof(T) == typeof(char) && length <= int.MaxValue ? BuildString(first, (int)length) : GetType().ToString();

        static void FillChars(Span<char> output, MemoryChunk? chunk)
        {
            Debug.Assert(typeof(T) == typeof(char));

            for (ReadOnlySpan<T> input; chunk is not null; output = output.Slice(input.Length), chunk = chunk.Next)
            {
                input = chunk.WrittenMemory.Span;
                ref var firstChar = ref Unsafe.As<T, char>(ref GetReference(input));
                CreateReadOnlySpan(ref firstChar, input.Length).CopyTo(output);
            }
        }

        static string BuildString(MemoryChunk? first, int length)
        {
            string result;

            if (length is 0)
            {
                result = string.Empty;
            }
            else
            {
                result = new('\0', length);
                FillChars(CreateSpan(ref Unsafe.AsRef(in result.GetPinnableReference()), length), first);
            }

            return result;
        }
    }
}

file static class SparseBufferWriter
{
    internal static int LinearGrowth(int chunkSize, ref int chunkIndex) => Math.Max(chunkSize * ++chunkIndex, chunkSize);

    internal static int ExponentialGrowth(int chunkSize, ref int chunkIndex) => Math.Max(chunkSize << ++chunkIndex, chunkSize);

    internal static int NoGrowth(int chunkSize, ref int chunkIndex)
    {
        Debug.Assert(chunkIndex == 0);
        return chunkSize;
    }
}