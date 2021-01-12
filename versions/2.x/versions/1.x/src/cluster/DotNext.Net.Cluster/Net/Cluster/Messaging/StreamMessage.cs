using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents message which content is represented by <see cref="Stream"/>.
    /// </summary>
    public class StreamMessage : StreamTransferObject, IDisposableMessage
    {
        /// <summary>
        /// Initializes a new message.
        /// </summary>
        /// <param name="content">The message content.</param>
        /// <param name="leaveOpen"><see langword="true"/> to leave the stream open after <see cref="StreamMessage"/> object is disposed; otherwise, <see langword="false"/>.</param>
        /// <param name="name">The name of the message.</param>
        /// <param name="type">Media type of the message.</param>
        public StreamMessage(Stream content, bool leaveOpen, string name, ContentType type = null)
            : base(content, leaveOpen)
        {
            Name = name;
            Type = type ?? new ContentType(MediaTypeNames.Application.Octet);
        }

        /// <summary>
        /// Creates copy of the original message stored in the managed heap.
        /// </summary>
        /// <param name="message">The origin message.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The message which stores the content of the original message in the memory.</returns>
        public static async Task<StreamMessage> CreateBufferedMessageAsync(IMessage message, CancellationToken token = default)
        {
            var content = new MemoryStream(2048);
            await message.CopyToAsync(content, token).ConfigureAwait(false);
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
    }
}
