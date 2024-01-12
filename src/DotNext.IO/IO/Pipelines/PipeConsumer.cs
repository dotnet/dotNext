using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.IO.Pipelines;

using Buffers;

[StructLayout(LayoutKind.Auto)]
internal readonly struct PipeConsumer(PipeWriter writer) : IReadOnlySpanConsumer<byte>
{
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private static async ValueTask Write(PipeWriter output, ReadOnlyMemory<byte> input, CancellationToken token)
    {
        var result = await output.WriteAsync(input, token).ConfigureAwait(false);
        result.ThrowIfCancellationRequested(token);
    }

    ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
        => Write(writer, input, token);

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> span) => writer.Write(span);

    public static implicit operator PipeConsumer(PipeWriter writer) => new(writer);
}