using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging;

using Buffers;
using IO;
using static Mime.ContentTypeExtensions;

/// <summary>
/// Represents text message.
/// </summary>
public class TextMessage : IMessage
{
    /// <summary>
    /// Initializes a new text message.
    /// </summary>
    /// <param name="value">The message content.</param>
    /// <param name="name">The name of the message.</param>
    public TextMessage(string value, string name)
        : this(name, value, null)
    {
    }

    internal TextMessage(string name, string value, string? mediaType)
    {
        Content = value;
        Type = new ContentType()
        {
            MediaType = mediaType is { Length: > 0 } ? mediaType : MediaTypeNames.Text.Plain,
            CharSet = "utf-8",
        };
        Name = name;
    }

    /// <summary>
    /// Gets name of this message.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets content length, in bytes.
    /// </summary>
    public int Length => Type.GetEncoding().GetByteCount(Content);

    /// <inheritdoc/>
    bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc/>
    long? IDataTransferObject.Length => Length;

    /// <summary>
    /// The message content.
    /// </summary>
    public string Content { get; }

    /// <inheritdoc/>
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
    {
        ValueTask result;
        var bufferWriter = writer.TryGetBufferWriter();
        if (bufferWriter is null)
        {
            result = writer.WriteStringAsync(Content.AsMemory(), Type.GetEncoding(), null, token);
        }
        else
        {
            // fast path - serialize synchronously
            result = ValueTask.CompletedTask;
            try
            {
                bufferWriter.Encode(Content.AsSpan(), Type.GetEncoding());
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    /// <summary>
    /// MIME type of the message.
    /// </summary>
    public ContentType Type { get; }
}