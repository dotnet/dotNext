using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.IO.Pipelines;

using Runtime;
using Runtime.CompilerServices;

[StructLayout(LayoutKind.Auto)]
internal readonly struct PipeConsumer(PipeWriter writer) : IConsumer<ReadOnlySpan<byte>>, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
{
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private static async ValueTask Write(PipeWriter output, ReadOnlyMemory<byte> input, CancellationToken token)
    {
        var result = await output.WriteAsync(input, token).ConfigureAwait(false);
        result.ThrowIfCancellationRequested(token);
    }

    ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
        => Write(writer, input, token);

    void IConsumer<ReadOnlySpan<byte>>.Invoke(ReadOnlySpan<byte> span) => writer.Write(span);

    public static implicit operator PipeConsumer(PipeWriter writer) => new(writer);

    void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
        => throw new NotSupportedException();
}