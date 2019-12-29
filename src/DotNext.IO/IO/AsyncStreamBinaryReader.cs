using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using DecodingContext = Text.DecodingContext;

    /// <summary>
    /// Represents binary reader for the stream.
    /// </summary>
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
            if((await input.ReadAsync(output, token).ConfigureAwait(false)) != output.Length)
                throw new EndOfStreamException();
        }

        public ValueTask ReadAsync(Memory<byte> output, CancellationToken token = default) => ReadAsync(input, output, token);

        public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token = default)
            => StreamExtensions.ReadStringAsync(input, length, context, buffer, token);
        
        public ValueTask<string> ReadStringAsync(StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token = default)
            => StreamExtensions.ReadStringAsync(input, lengthFormat, context, buffer, token);
    }
}