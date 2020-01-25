using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using DecodingContext = Text.DecodingContext;

    internal sealed class EmptyBinaryReader : IAsyncBinaryReader
    {
        private static ValueTask<T> EndOfStream<T>()
            => new ValueTask<T>(Task.FromException<T>(new EndOfStreamException()));

        private static ValueTask EndOfStream()
            => new ValueTask(Task.FromException(new EndOfStreamException()));

        public ValueTask<T> ReadAsync<T>(CancellationToken token)
            where T : unmanaged
            => EndOfStream<T>();

        public ValueTask ReadAsync(Memory<byte> output, CancellationToken token)
            => EndOfStream();

        public ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token)
            => EndOfStream<string>();

        public ValueTask<string> ReadStringAsync(StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token)
            => EndOfStream<string>();

        public Task CopyToAsync(Stream output, CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

        public Task CopyToAsync(PipeWriter output, CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;
    }
}