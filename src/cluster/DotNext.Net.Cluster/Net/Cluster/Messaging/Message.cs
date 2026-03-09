using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging;

using IO;
using Runtime.Serialization;

/// <summary>
/// Represents typed message.
/// </summary>
/// <typeparam name="T">The payload of the message.</typeparam>
internal sealed class Message<T>() : IMessage
    where T : ISerializable<T>
{
    internal Message(string? mediaType)
        : this()
        => Type = mediaType is { Length: > 0 } ? new(mediaType) : new(MediaTypeNames.Application.Octet);

    /// <summary>
    /// Gets payload of this message.
    /// </summary>
    public required T Payload { get; init; }

    /// <summary>
    /// Gets name of this message.
    /// </summary>
    public required string Name
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrEmpty(value);

            field = value;
        }
    } = string.Empty;

    /// <summary>
    /// Gets MIME type of this message.
    /// </summary>
    public ContentType Type
    {
        get => field ??= new(MediaTypeNames.Application.Octet);
        init;
    }

    /// <inheritdoc/>
    long? IDataTransferObject.Length => Payload.Length;

    /// <inheritdoc/>
    bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc/>
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => Payload.WriteToAsync(writer, token);
}