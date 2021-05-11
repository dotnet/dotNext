using System;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    using Buffers;
    using IO;
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

        /// <inheritdoc/>
        bool IDataTransferObject.IsReusable => true;

        /// <inheritdoc/>
        long? IDataTransferObject.Length => Length;

        /// <summary>
        /// The message content.
        /// </summary>
        public string Content { get; }

        /// <inheritdoc/>
        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            ValueTask result;
            var bufferWriter = writer.TryGetBufferWriter();
            if (bufferWriter is null)
            {
                result = writer.WriteAsync(Content.AsMemory(), Type.GetEncoding(), null, token);
            }
            else
            {
                // fast path - serialize synchronously
                result = new();
                try
                {
                    bufferWriter.WriteString(Content.AsSpan(), Type.GetEncoding());
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new(Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        /// <summary>
        /// MIME type of the message.
        /// </summary>
        public ContentType Type { get; }
    }
}