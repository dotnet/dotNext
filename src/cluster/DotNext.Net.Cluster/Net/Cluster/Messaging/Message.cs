using System;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    using IO;
    using Runtime.Serialization;

    /// <summary>
    /// Represents typed message.
    /// </summary>
    /// <typeparam name="T">The payload of the message.</typeparam>
    public sealed class Message<T> : IMessage
    {
        private readonly IFormatter<T> formatter;

        /// <summary>
        /// Initializes a new message.
        /// </summary>
        /// <param name="name">The name of the message.</param>
        /// <param name="payload">The payload of the message.</param>
        /// <param name="formatter">The payload serializer.</param>
        /// <param name="type">MIME type of the message.</param>
        public Message(string name, T payload, IFormatter<T> formatter, string? type = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            this.formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
            Type = new ContentType(type ?? MediaTypeNames.Application.Octet);
            Payload = payload;
        }

        /// <summary>
        /// Gets payload of this message.
        /// </summary>
        public T Payload { get; }

        /// <summary>
        /// Gets name of this message.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets MIME type of this message.
        /// </summary>
        public ContentType Type { get; }

        /// <inheritdoc/>
        long? IDataTransferObject.Length => formatter.GetLength(Payload);

        /// <inheritdoc/>
        bool IDataTransferObject.IsReusable => true;

        /// <inheritdoc/>
        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => formatter.SerializeAsync(Payload, writer, token);
    }
}