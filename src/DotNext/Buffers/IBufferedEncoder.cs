using System.Runtime.CompilerServices;

namespace DotNext.Buffers;

internal interface IBufferedEncoder : IResettable
{
    bool HasBufferedData { get; }

    static abstract int MaxBufferedDataSize { get; }
}

internal interface IBufferedEncoder<TOutput> : IBufferedEncoder
{
    MemoryOwner<TOutput> Encode(ReadOnlySpan<byte> input, MemoryAllocator<TOutput>? allocator);

    int Flush(Span<TOutput> buffer);
    
    protected static async IAsyncEnumerable<ReadOnlyMemory<TOutput>> EncodeAsync<TEncoder>(IAsyncEnumerable<ReadOnlyMemory<byte>> bytes, MemoryAllocator<TOutput>? allocator, [EnumeratorCancellation] CancellationToken token)
        where TEncoder : struct, IBufferedEncoder<TOutput>
    {
        var encoder = new TEncoder();
        MemoryOwner<TOutput> buffer;

        await foreach (var chunk in bytes.WithCancellation(token).ConfigureAwait(false))
        {
            buffer = encoder.Encode(chunk.Span, allocator);
            try
            {
                yield return buffer.Memory;
            }
            finally
            {
                buffer.Dispose();
            }
        }

        if (encoder.HasBufferedData)
        {
            buffer = allocator.AllocateAtLeast(TEncoder.MaxBufferedDataSize);
            try
            {
                var count = encoder.Flush(buffer.Span);
                yield return buffer.Memory.Slice(0, count);
            }
            finally
            {
                buffer.Dispose();
            }
        }
    }
}