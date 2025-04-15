using System.Runtime.CompilerServices;

namespace DotNext.Buffers;

internal interface IBufferedDecoder : IResettable
{
    bool NeedMoreData { get; }

    static abstract FormatException CreateFormatException();
}

internal interface IBufferedDecoder<TInput> : IBufferedDecoder
{
    MemoryOwner<byte> Decode(ReadOnlySpan<TInput> input, MemoryAllocator<byte>? allocator);

    protected static async IAsyncEnumerable<ReadOnlyMemory<byte>> DecodeAsync<TDecoder>(IAsyncEnumerable<ReadOnlyMemory<TInput>> chars,
        MemoryAllocator<byte>? allocator, [EnumeratorCancellation] CancellationToken token)
        where TDecoder : struct, IBufferedDecoder<TInput>
    {
        var decoder = new TDecoder();

        await foreach (var chunk in chars.WithCancellation(token).ConfigureAwait(false))
        {
            var buffer = decoder.Decode(chunk.Span, allocator);
            try
            {
                yield return buffer.Memory;
            }
            finally
            {
                buffer.Dispose();
            }
        }

        if (decoder.NeedMoreData)
            throw TDecoder.CreateFormatException();
    }
}