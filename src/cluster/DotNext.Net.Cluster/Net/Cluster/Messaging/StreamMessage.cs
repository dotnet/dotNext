using System;
using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    public sealed class StreamMessage : Disposable, IMessage
    {
        private const int BufferSize = 1024;

        private readonly Stream content;
        private readonly bool disposeStream;

        public StreamMessage(Stream content, bool leaveOpen, string name, ContentType type = null)
        {
            disposeStream = !leaveOpen;
            this.content = content;
            Name = name;
            Type = type ?? new ContentType(MediaTypeNames.Application.Octet);
        }

        public string Name { get; }

        long? IMessage.Length => content.CanSeek ? content.Length : default(long?);

        public ContentType Type { get; }

        Task IMessage.CopyToAsync(Stream output) => content.CopyToAsync(output);

        async ValueTask IMessage.CopyToAsync(PipeWriter output, CancellationToken token)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            var buffer = new byte[BufferSize];
            int count;
            while ((count = await content.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                var result = await output.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, count), token).ConfigureAwait(false);
                if (result.IsCanceled || result.IsCompleted)
                    break;
                result = await output.FlushAsync(token).ConfigureAwait(false);
                if (result.IsCanceled || result.IsCompleted)
                    break;
            }
            output.Complete(token.IsCancellationRequested ? new OperationCanceledException(token) : null);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && disposeStream)
                content.Dispose();
            base.Dispose(disposing);
        }
    }
}
