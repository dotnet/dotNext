using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

[StructLayout(LayoutKind.Auto)]
internal struct InlineBufferWriter<T>(MemoryAllocator<T>? allocator) : IGrowableBuffer<T>
{
    private readonly MemoryAllocator<T> allocator = allocator.DefaultIfNull;
    private MemoryOwner<T> buffer;
    private int position;

    readonly long IGrowableBuffer<T>.WrittenCount => WrittenCount;

    public int Capacity
    {
        readonly get => buffer.Length;
        init => buffer = allocator.AllocateAtLeast(value);
    }

    public void Write(ReadOnlySpan<T> input)
        => Memory.Write<T, Ref>(new(ref this), input);

    readonly void IGrowableBuffer<T>.CopyTo<TConsumer>(TConsumer consumer)
        => consumer.Invoke(WrittenMemory.Span);

    readonly ValueTask IGrowableBuffer<T>.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
        => consumer.Invoke(WrittenMemory, token);

    readonly int IGrowableBuffer<T>.CopyTo(Span<T> output)
        => WrittenMemory.Span >> output;

    public void Reset()
    {
        buffer.Dispose();
        buffer = default;
    }

    readonly bool IGrowableBuffer<T>.TryGetWrittenContent(out ReadOnlyMemory<T> block)
    {
        block = WrittenMemory;
        return true;
    }

    public readonly int WrittenCount => position;

    public readonly ReadOnlyMemory<T> WrittenMemory => buffer.Memory.Slice(0, position);

    public void Advance(int count)
    {
        var newPosition = position + count;
        if ((uint)newPosition > (uint)buffer.Length)
            InvalidOperationException.Throw();

        position = newPosition;
    }

    public Memory<T> GetMemory(int sizeHint) => GetBuffer(sizeHint).Memory.Slice(position);

    public Span<T> GetSpan(int sizeHint) => GetBuffer(sizeHint).Span.Slice(position);
    
    [UnscopedRef]
    private ref readonly MemoryOwner<T> GetBuffer(int sizeHint)
    {
        if (IGrowableBuffer<T>.GetBufferSize(sizeHint, buffer.Length, position, out sizeHint))
            buffer.Resize(sizeHint, allocator);
        
        return ref buffer;
    }
    
    public MemoryOwner<T> DetachBuffer()
    {
        MemoryOwner<T> result;

        if (position is 0)
        {
            result = default;
        }
        else
        {
            result = buffer;
            result.Truncate(position);
            position = 0;
            buffer = default;
        }

        return result;
    }

    public void Dispose() => buffer.Dispose();

    public readonly override string ToString() => WrittenMemory.ToString();

    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct Ref(ref InlineBufferWriter<T> writer) : IBufferWriter<T>, ITypedReference<InlineBufferWriter<T>>
    {
        private readonly ref InlineBufferWriter<T> writer = ref writer;

        void IBufferWriter<T>.Advance(int count) => writer.Advance(count);

        Memory<T> IBufferWriter<T>.GetMemory(int sizeHint) => writer.GetMemory(sizeHint);

        Span<T> IBufferWriter<T>.GetSpan(int sizeHint) => writer.GetSpan(sizeHint);

        ref readonly InlineBufferWriter<T> ITypedReference<InlineBufferWriter<T>>.Value => ref writer;
    }
}