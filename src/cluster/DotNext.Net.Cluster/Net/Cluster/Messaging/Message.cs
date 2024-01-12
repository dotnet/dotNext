using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging;

using IO;
using Runtime.Serialization;

/// <summary>
/// Represents typed message.
/// </summary>
/// <typeparam name="T">The payload of the message.</typeparam>
internal sealed class Message<T>() : IMessage
    where T : notnull, ISerializable<T>
{
    private readonly string name = string.Empty;
    private ContentType? type;

    internal Message(string? mediaType)
        : this()
        => type = mediaType is { Length: > 0 } ? new(mediaType) : new(MediaTypeNames.Application.Octet);

    /// <summary>
    /// Gets payload of this message.
    /// </summary>
    required public T Payload { get; init; }

    /// <summary>
    /// Gets name of this message.
    /// </summary>
    required public string Name
    {
        get => name;
        init
        {
            ArgumentException.ThrowIfNullOrEmpty(value);

            name = value;
        }
    }

    /// <summary>
    /// Gets MIME type of this message.
    /// </summary>
    public ContentType Type
    {
        get => type ??= new(MediaTypeNames.Application.Octet);
        init => type = value;
    }

    /// <inheritdoc/>
    long? IDataTransferObject.Length => Payload.Length;

    /// <inheritdoc/>
    bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc/>
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => Payload.WriteToAsync(writer, token);
}