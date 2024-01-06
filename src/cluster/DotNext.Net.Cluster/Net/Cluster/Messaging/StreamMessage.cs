using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging;

using IO;

/// <summary>
/// Represents message which content is represented by <see cref="Stream"/>.
/// </summary>
/// <param name="content">The message content.</param>
/// <param name="leaveOpen"><see langword="true"/> to leave the stream open after <see cref="StreamMessage"/> object is disposed; otherwise, <see langword="false"/>.</param>
/// <param name="name">The name of the message.</param>
/// <param name="type">Media type of the message.</param>
public class StreamMessage(Stream content, bool leaveOpen, string name, ContentType? type = null) : StreamTransferObject(content, leaveOpen), IDisposableMessage
{
    /// <summary>
    /// Gets name of this message.
    /// </summary>
    public string Name => name;

    /// <summary>
    /// Gets media type of this message.
    /// </summary>
    public ContentType Type { get; } = type ?? new ContentType(MediaTypeNames.Application.Octet);
}