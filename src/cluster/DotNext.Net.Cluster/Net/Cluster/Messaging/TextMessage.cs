using System;
using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    using Buffers;
    using static Mime.ContentTypeExtensions;

    /// <summary>
    /// Represents text message.
    /// </summary>
    public class TextMessage : IMessage
    {
        /// <summary>
        /// Initializes a new text message.
        /// </summary>
        /// <param name="value">The message content.</param>
        /// <param name="name">The name of the message.</param>
        public TextMessage(string value, string name)
            : this(name, value, null)
        {

        }

        internal TextMessage(string name, string value, string mediaType)
        {
            Content = value;
            Type = new ContentType() { MediaType = mediaType.IfNullOrEmpty(MediaTypeNames.Text.Plain), CharSet = "utf-8" };
            Name = name;
        }

        /// <summary>
        /// Gets name of this message.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets content length, in bytes.
        /// </summary>
        public int Length => Type.GetEncoding().GetByteCount(Content);

        bool IDataTransferObject.IsReusable => true;

        long? IDataTransferObject.Length => Length;

        /// <summary>
        /// The message content.
        /// </summary>
        public string Content { get; }

        async Task IDataTransferObject.CopyToAsync(Stream output, CancellationToken token)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            const int charsBufferSize = 512;
            var encoding = Type.GetEncoding();
            using (var buffer = new ArrayRental<byte>(encoding.GetMaxByteCount(charsBufferSize)))
            {
                var offset = 0;
                do
                {
                    var n = encoding.GetBytes(Content, offset, Math.Min(Content.Length - offset, charsBufferSize), buffer, 0);
                    await output.WriteAsync(buffer, 0, n).ConfigureAwait(false);
                    offset += n;
                }
                while (offset < Content.Length);
            }
        }

        async ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            var encoding = Type.GetEncoding();
            foreach (var chunk in Content.Split(512))
            {
                var bytes = new ReadOnlyMemory<byte>(encoding.GetBytes(chunk.ToArray()));
                var result = await output.WriteAsync(bytes, token);
                if (result.IsCompleted)
                    break;
                if (result.IsCanceled)
                    throw new OperationCanceledException(token);
                result = await output.FlushAsync(token);
                if (result.IsCompleted)
                    break;
                if (result.IsCanceled)
                    throw new OperationCanceledException(token);
            }
        }

        /// <summary>
        /// MIME type of the message.
        /// </summary>
        public ContentType Type { get; }
    }
}