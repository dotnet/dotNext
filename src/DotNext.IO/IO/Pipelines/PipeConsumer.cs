using System;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO.Pipelines
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct PipeConsumer : ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        private readonly PipeWriter writer;

        internal PipeConsumer(PipeWriter writer) => this.writer = writer ?? throw new ArgumentNullException(nameof(writer));

        private static async ValueTask Write(PipeWriter output, ReadOnlyMemory<byte> input, CancellationToken token)
        {
            var result = await output.WriteAsync(input, token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(token);
        }

        ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
            => Write(writer, input, token);

        public static implicit operator PipeConsumer(PipeWriter writer) => new(writer);
    }
}