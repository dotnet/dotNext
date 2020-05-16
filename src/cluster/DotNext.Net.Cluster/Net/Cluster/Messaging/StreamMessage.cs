using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging
{
    using IO;

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
        public StreamMessage(Stream content, bool leaveOpen, string name, ContentType? type = null)
            : base(content, leaveOpen)
        {
            Name = name;
            Type = type ?? new ContentType(MediaTypeNames.Application.Octet);
        }

        /// <summary>
        /// Initializes a new empty message of predefined size.
        /// </summary>
        /// <param name="name">The name of the message.</param>
        /// <param name="type">Media type of the message.</param>
        /// <param name="size">The initial size of the message, in bytes.</param>
        /// <param name="growable"><see langword="true"/> if the size is not fixed and can be expanded if necessary; <see langword="false"/> to use strictly limited memory size.</param>
        [SuppressMessage("Reliability", "CA2000", Justification = "Stream lifetime is controlled by StreamMessage")]
        public StreamMessage(string name, ContentType type, int size = 1024, bool growable = true)
            : this(growable ? new MemoryStream(size) : new RentedMemoryStream(size), false, name, type)
        {
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
