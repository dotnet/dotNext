using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using Text;

    /// <summary>
    /// Represents binary reader for the stream.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct AsyncStreamBinaryReader : IAsyncBinaryReader
    {
        private readonly Memory<byte> buffer;
        private readonly Stream input;

        internal AsyncStreamBinaryReader(Stream input, Memory<byte> buffer)
        {
            this.input = input;
            this.buffer = buffer;
        }

        public ValueTask<T> ReadAsync<T>(CancellationToken token = default)
            where T : unmanaged
            => StreamExtensions.ReadAsync<T>(input, buffer, token);

        private static async ValueTask ReadAsync(Stream input, Memory<byte> output, CancellationToken token)
        {
            if ((await input.ReadAsync(output, token).ConfigureAwait(false)) != output.Length)
                throw new EndOfStreamException();
        }

        public ValueTask ReadAsync(Memory<byte> output, CancellationToken token = default) => ReadAsync(input, output, token);

        public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token = default)
            => StreamExtensions.ReadStringAsync(input, length, context, buffer, token);

        public ValueTask<string> ReadStringAsync(StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token = default)
            => StreamExtensions.ReadStringAsync(input, lengthFormat, context, buffer, token);

        Task IAsyncBinaryReader.CopyToAsync(Stream output, CancellationToken token)
            => input.CopyToAsync(output, token);

        Task IAsyncBinaryReader.CopyToAsync(PipeWriter output, CancellationToken token)
            => input.CopyToAsync(output, token);
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct AsyncStreamBinaryWriter : IAsyncBinaryWriter
    {
        private readonly Memory<byte> buffer;
        private readonly Stream output;

        internal AsyncStreamBinaryWriter(Stream output, Memory<byte> buffer)
        {
            this.output = output;
            this.buffer = buffer;
        }

        public ValueTask WriteAsync<T>(T value, CancellationToken token)
            where T : unmanaged
            => output.WriteAsync(value, buffer, token);

        public ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token)
            => output.WriteAsync(input, token);

        public ValueTask WriteAsync(ReadOnlyMemory<char> chars, EncodingContext context, StringLengthEncoding? lengthFormat, CancellationToken token)
            => output.WriteStringAsync(chars, context, buffer, lengthFormat, token);

        Task IAsyncBinaryWriter.CopyFromAsync(Stream input, CancellationToken token)
            => input.CopyToAsync(output, token);

        Task IAsyncBinaryWriter.CopyFromAsync(PipeReader input, CancellationToken token)
            => input.CopyToAsync(output, token);
    }
}