using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging;

using IO;
using Text.Json;

/// <summary>
/// Represents JSON-serializable message.
/// </summary>
/// <typeparam name="T">JSON-serializable type.</typeparam>
public sealed class JsonMessage<T> : IMessage
    where T : IJsonSerializable<T>
{
    /// <summary>
    /// Gets the name of this message.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the content of this message.
    /// </summary>
    public required T Content { get; init; }

    /// <inheritdoc />
    ContentType IMessage.Type { get; } = new(MediaTypeNames.Application.Json);

    /// <inheritdoc />
    bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc />
    long? IDataTransferObject.Length => null;

    /// <inheritdoc />
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => JsonSerializable<T>.SerializeAsync(writer, Content, token);
}