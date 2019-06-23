using System.Buffers;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    using Buffers;
    using static Mime.ContentTypeExtensions;

    /// <summary>
    /// Represents text message.
    /// </summary>
    public sealed class TextMessage : IMessage<string>
    {
        /// <summary>
        /// Initializes a new text message.
        /// </summary>
        /// <param name="name">The name of the message.</param>
        /// <param name="value">The message content.</param>
        public TextMessage(string name, string value)
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
        public long Length => Type.GetEncoding().GetByteCount(Content);

        long? IMessage.Length => Length;

        /// <summary>
        /// The message content.
        /// </summary>
        public string Content { get; }

        async Task IMessage.CopyToAsync(Stream output)
        {
            using (var writer = new StreamWriter(output, Type.GetEncoding(), 1024, true) { AutoFlush = true })
                await writer.WriteAsync(Content).ConfigureAwait(false);
        }

        /// <summary>
        /// MIME type of the message.
        /// </summary>
        public ContentType Type { get; }
    }
}