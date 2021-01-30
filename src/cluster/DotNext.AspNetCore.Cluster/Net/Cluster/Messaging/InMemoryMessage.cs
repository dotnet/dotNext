using System;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    using Buffers;
    using IO;

    // this is not a public class because it's designed for special purpose: bufferize content of another DTO.
    // For that purpose we using growable buffer which relies on the pooled memory
    internal sealed class InMemoryMessage : Disposable, IDataTransferObject, IBufferedMessage
    {
        [StructLayout(LayoutKind.Auto)]
        private readonly struct BufferDecoder : IDataTransferObject.ITransformation<BufferWriter<byte>>
        {
            private readonly int initialSize;

            internal BufferDecoder(int initialSize) => this.initialSize = initialSize;

            public async ValueTask<BufferWriter<byte>> TransformAsync<TReader>(TReader reader, CancellationToken token)
                where TReader : notnull, IAsyncBinaryReader
            {
                var writer = new PooledArrayBufferWriter<byte>(initialSize);
                await reader.CopyToAsync(writer).ConfigureAwait(false);
                return writer;
            }
        }

        private readonly int initialSize;
        private BufferWriter<byte>? buffer;

        internal InMemoryMessage(string name, ContentType type, int initialSize)
        {
            Name = name;
            Type = type;
            this.initialSize = initialSize;
        }

        public string Name { get; }

        public ContentType Type { get; }

        bool IDataTransferObject.IsReusable => true;

        long? IDataTransferObject.Length => buffer?.WrittenCount ?? 0L;

        private ReadOnlyMemory<byte> Content => buffer?.WrittenMemory ?? ReadOnlyMemory<byte>.Empty;

        public ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            where TWriter : IAsyncBinaryWriter
            => writer.WriteAsync(Content, null, token);

        async ValueTask IBufferedMessage.LoadFromAsync(IDataTransferObject source, CancellationToken token)
        {
            buffer?.Dispose();
            buffer = await source.TransformAsync<BufferWriter<byte>, BufferDecoder>(new BufferDecoder(initialSize), token).ConfigureAwait(false);
        }

        void IBufferedMessage.PrepareForReuse()
        {
        }

        ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
            => transformation.TransformAsync(IAsyncBinaryReader.Create(Content), token);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                buffer?.Dispose();
                buffer = null;
            }

            base.Dispose(disposing);
        }

        ValueTask IAsyncDisposable.DisposeAsync() => DisposeAsync(false);
    }
}