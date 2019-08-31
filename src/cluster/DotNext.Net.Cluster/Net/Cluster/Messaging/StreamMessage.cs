using System;
using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents message which content is represented by <see cref="Stream"/>.
    /// </summary>
    public class StreamMessage : Disposable, IDisposableMessage
    {
        private const int BufferSize = 1024;
        private readonly bool leaveOpen;
        private readonly Stream content;

        /// <summary>
        /// Initializes a new message.
        /// </summary>
        /// <param name="content">The message content.</param>
        /// <param name="leaveOpen"><see langword="true"/> to leave the stream open after <see cref="StreamMessage"/> object is disposed; otherwise, <see langword="false"/>.</param>
        /// <param name="name">The name of the message.</param>
        /// <param name="type">Media type of the message.</param>
        public StreamMessage(Stream content, bool leaveOpen, string name, ContentType type = null)
        {
            this.leaveOpen = leaveOpen;
            Name = name;
            Type = type ?? new ContentType(MediaTypeNames.Application.Octet);
            this.content = content;
        }

        /// <summary>
        /// Creates copy of the original message stored in the managed heap.
        /// </summary>
        /// <param name="message">The origin message.</param>
        /// <returns>The message which stores the content of the original message in the memory.</returns>
        public static async Task<StreamMessage> CreateBufferedMessageAsync(IMessage message)
        {
            var content = new MemoryStream(2048);
            await message.CopyToAsync(content).ConfigureAwait(false);
            content.Seek(0, SeekOrigin.Begin);
            return new StreamMessage(content, false, message.Name, message.Type);
        }

        /// <summary>
        /// Gets name of this message.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets media type of this message.
        /// </summary>
        public ContentType Type { get; }

        /// <summary>
        /// Indicates that the content of this message can be copied to the output stream or pipe multiple times.
        /// </summary>
        public bool IsReusable => content.CanSeek;

        long? IMessage.Length => content.CanSeek ? content.Length : default(long?);

        private static async Task CopyToAsyncAndSeek(Stream input, Stream output)
        {
            await input.CopyToAsync(output).ConfigureAwait(false);
            input.Seek(0, SeekOrigin.Begin);
        }

        Task IMessage.CopyToAsync(Stream output) =>
            content.CanSeek ? CopyToAsyncAndSeek(content, output) : content.CopyToAsync(output);

        ValueTask IMessage.CopyToAsync(PipeWriter output, CancellationToken token)
            => CopyToAsync(content, output, true, token);

        internal static async ValueTask CopyToAsync(Stream source, PipeWriter output, bool resetStream, CancellationToken token)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            var buffer = new byte[BufferSize];
            int count;
            while ((count = await source.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
            {
                var result = await output.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, count), token).ConfigureAwait(false);
                if (result.IsCompleted)
                    break;
                if (result.IsCanceled)
                    throw new OperationCanceledException(token);
                result = await output.FlushAsync(token).ConfigureAwait(false);
                if (result.IsCompleted)
                    break;
                if (result.IsCanceled)
                    throw new OperationCanceledException(token);
            }

            if (resetStream && source.CanSeek)
                source.Seek(0, SeekOrigin.Begin);
        }

        /// <summary>
        /// Releases resources associated with this message.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if called from <see cref="Disposable.Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Disposable.Finalize()"/>.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !leaveOpen)
                content.Dispose();
            base.Dispose(disposing);
        }
    }
}
