using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;
using IO.Log;

/// <summary>
/// Represents a log entry with binary payload.
/// </summary>
/// <typeparam name="T">Binary-formattable type.</typeparam>
[StructLayout(LayoutKind.Auto)]
[RequiresPreviewFeatures]
public struct BinaryLogEntry<T> : IBinaryLogEntry
    where T : struct, IBinaryFormattable<T>
{
    private readonly long term;
    private readonly int? commandId;

    /// <summary>
    /// Initializes a new binary log entry.
    /// </summary>
    public BinaryLogEntry()
    {
        Timestamp = DateTimeOffset.UtcNow;
        Content = default;
        term = default;
        commandId = default;
    }

    /// <summary>
    /// Gets or sets the log entry payload.
    /// </summary>
    public T Content;

    /// <summary>
    /// Gets the timestamp of this log entry.
    /// </summary>
    public readonly DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets Term value associated with this log entry.
    /// </summary>
    public long Term
    {
        readonly get => term;
        init => term = value;
    }

    /// <summary>
    /// Gets the command identifier.
    /// </summary>
    public int? CommandId
    {
        readonly get => commandId;
        init => commandId = value;
    }

    /// <inheritdoc />
    readonly bool ILogEntry.IsSnapshot => false;

    /// <inheritdoc />
    readonly bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc />
    readonly long? IDataTransferObject.Length => T.Size;

    /// <inheritdoc />
    readonly MemoryOwner<byte> IBinaryLogEntry.ToBuffer(MemoryAllocator<byte> allocator)
    {
        var buffer = allocator.Invoke(T.Size, exactSize: true);
        var writer = new SpanWriter<byte>(buffer.Span);
        Content.Format(ref writer);
        return buffer;
    }

    /// <inheritdoc />
    readonly ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => writer.WriteFormattableAsync(Content, token);
}