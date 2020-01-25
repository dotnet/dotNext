using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO.Pipelines
{
    using Text;

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct PipeBinaryReader : IAsyncBinaryReader
    {
        private readonly PipeReader input;

        internal PipeBinaryReader(PipeReader reader) => input = reader;

        public ValueTask<T> ReadAsync<T>(CancellationToken token)
            where T : unmanaged
            => input.ReadAsync<T>(token);

        public ValueTask ReadAsync(Memory<byte> output, CancellationToken token)
            => input.ReadAsync(output, token);

        public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token)
            => input.ReadStringAsync(length, context, token);

        public ValueTask<string> ReadStringAsync(StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token)
            => input.ReadStringAsync(lengthFormat, context, token);

        Task IAsyncBinaryReader.CopyToAsync(Stream output, CancellationToken token)
            => input.CopyToAsync(output, token);

        Task IAsyncBinaryReader.CopyToAsync(PipeWriter output, CancellationToken token)
            => input.CopyToAsync(output, token);
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct PipeBinaryWriter : IAsyncBinaryWriter
    {
        private readonly PipeWriter output;

        internal PipeBinaryWriter(PipeWriter writer) => output = writer;

        public async ValueTask WriteAsync<T>(T value, CancellationToken token)
            where T : unmanaged
        {
            var result = await output.WriteAsync(value, token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested();
        }

        public async ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token)
        {
            var result = await output.WriteAsync(input, token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested();
        }

        public ValueTask WriteAsync(ReadOnlyMemory<char> chars, EncodingContext context, StringLengthEncoding? lengthFormat, CancellationToken token)
            => output.WriteStringAsync(chars, context, lengthFormat: lengthFormat, token: token);

        Task IAsyncBinaryWriter.CopyFromAsync(Stream input, CancellationToken token)
            => input.CopyToAsync(output, token);

        Task IAsyncBinaryWriter.CopyFromAsync(PipeReader input, CancellationToken token)
            => input.CopyToAsync(output, token);
    }
}