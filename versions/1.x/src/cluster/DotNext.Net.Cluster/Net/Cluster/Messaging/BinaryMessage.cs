using System;
using System.Buffers;
using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents binary message.
    /// </summary>
    public class BinaryMessage : BinaryTransferObject, IMessage
    {
        /// <summary>
        /// Initializes a new binary message.
        /// </summary>
        /// <param name="content">The content of the message.</param>
        /// <param name="name">The name of the message.</param>
        /// <param name="type">Media type of the message content.</param>
        public BinaryMessage(ReadOnlySequence<byte> content, string name, ContentType type = null)
            : base(content)
        {
            Type = type ?? new ContentType(MediaTypeNames.Application.Octet);
            Name = name;
        }

        /// <summary>
        /// Initializes a new binary message.
        /// </summary>
        /// <param name="content">The content of the message.</param>
        /// <param name="name">The name of the message.</param>
        /// <param name="type">Media type of the message content.</param>
        public BinaryMessage(ReadOnlyMemory<byte> content, string name, ContentType type = null)
            : this(new ReadOnlySequence<byte>(content), name, type)
        {
        }

        /// <summary>
        /// Gets name of the message.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets media type of the message.
        /// </summary>
        public ContentType Type { get; }
    }
}
