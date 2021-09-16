using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging;

using IO;
using Runtime.Serialization;

/// <summary>
/// Represents typed message.
/// </summary>
/// <typeparam name="T">The payload of the message.</typeparam>
internal sealed class Message<T> : IMessage
    where T : notnull, ISerializable<T>
{
    /// <summary>
    /// Initializes a new message.
    /// </summary>
    /// <param name="name">The name of the message.</param>
    /// <param name="payload">The payload of the message.</param>
    /// <param name="type">MIME type of the message.</param>
    public Message(string name, T payload, string? type = null)
        : this(name, payload, type is null ? null : new ContentType(type))
    {
    }

    /// <summary>
    /// Initializes a new message.
    /// </summary>
    /// <param name="name">The name of the message.</param>
    /// <param name="payload">The payload of the message.</param>
    /// <param name="type">MIME type of the message.</param>
    public Message(string name, T payload, ContentType? type)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? new ContentType(MediaTypeNames.Application.Octet);
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
    long? IDataTransferObject.Length => Payload.Length;

    /// <inheritdoc/>
    bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc/>
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => Payload.WriteToAsync(writer, token);
}