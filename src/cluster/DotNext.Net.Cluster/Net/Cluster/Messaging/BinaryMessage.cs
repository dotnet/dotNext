using System.IO;
using System.Net.Mime;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents binary message.
    /// </summary>
    public sealed class BinaryMessage : IMessage<Stream>
    {
        /// <summary>
        /// Initializes a new binary message.
        /// </summary>
        /// <param name="name">The name of the message.</param>
        /// <param name="content">The content of the message.</param>
        /// <param name="type">Media type of the message content.</param>
        public BinaryMessage(string name, byte[] content, ContentType type = null)
            : this(name, new MemoryStream(content), type ?? new ContentType(MediaTypeNames.Application.Octet))
        {
        }

        private BinaryMessage(string name, MemoryStream content, ContentType type)
        {
            Content = content;
            Type = type;
            Name = name;
        }

        /// <summary>
        /// Creates buffered message.
        /// </summary>
        /// <param name="message">The message to be converted into binary message.</param>
        /// <returns>The binary message representing buffered content of the original message.</returns>
        public static async Task<BinaryMessage> CreateBufferedMessageAsync(IMessage message)
        {
            switch(message)
            {
                case null:
                    return null;
                case BinaryMessage binary:
                    return binary;
                default:
                    var ms = new MemoryStream(1024);
                    await message.CopyToAsync(ms).ConfigureAwait(false);
                    ms.Seek(0, SeekOrigin.Begin);
                    return new BinaryMessage(message.Name, ms, message.Type);
            }
        }

        /// <summary>
        /// Gets stream representing content.
        /// </summary>
        public Stream Content { get; }

        /// <summary>
        /// Gets name of the message.
        /// </summary>
        public string Name { get; }

        long? IMessage.Length => Content.Length;

        /// <summary>
        /// Gets media type of the message.
        /// </summary>
        public ContentType Type { get; }

        Task IMessage.CopyToAsync(Stream output) => Content.CopyToAsync(output);
    }
}
