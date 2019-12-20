using System;
using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    using Buffers;
    using IO;
    using IO.Pipelines;
    using static Mime.ContentTypeExtensions;

    /// <summary>
    /// Represents text message.
    /// </summary>
    public class TextMessage : IMessage
    {
        private const int DefaultBufferSize = 128;

        /// <summary>
        /// Initializes a new text message.
        /// </summary>
        /// <param name="value">The message content.</param>
        /// <param name="name">The name of the message.</param>
        public TextMessage(string value, string name)
            : this(name, value, null)
        {

        }

        internal TextMessage(string name, string value, string? mediaType)
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

        async ValueTask IDataTransferObject.CopyToAsync(Stream output, CancellationToken token)
        {
            using var buffer = new ArrayRental<byte>(DefaultBufferSize);
            await output.WriteStringAsync(Content.AsMemory(), Type.GetEncoding(), buffer.Memory, null, token).ConfigureAwait(false);
        }

        ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token) => output.WriteStringAsync(Content.AsMemory(), Type.GetEncoding(), DefaultBufferSize, null, token);

        /// <summary>
        /// MIME type of the message.
        /// </summary>
        public ContentType Type { get; }
    }
}