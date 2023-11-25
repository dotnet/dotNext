using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;
using IO.Log;

/// <summary>
/// Represents a log entry with binary payload.
/// </summary>
/// <typeparam name="T">Binary-formattable type.</typeparam>
[StructLayout(LayoutKind.Auto)]
public struct BinaryLogEntry<T> : IBinaryLogEntry
    where T : struct, IBinaryFormattable<T>
{
    /// <summary>
    /// Initializes a new binary log entry.
    /// </summary>
    public BinaryLogEntry()
    {
    }

    /// <summary>
    /// Gets or sets the log entry payload.
    /// </summary>
    public T Content;

    /// <summary>
    /// Gets the timestamp of this log entry.
    /// </summary>
    public readonly DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets Term value associated with this log entry.
    /// </summary>
    public long Term
    {
        readonly get;
        init;
    }

    /// <summary>
    /// Gets the command identifier.
    /// </summary>
    public int? CommandId
    {
        readonly get;
        init;
    }

    /// <inheritdoc />
    readonly bool ILogEntry.IsSnapshot => false;

    /// <inheritdoc />
    readonly bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc />
    readonly long? IDataTransferObject.Length => T.Size;

    /// <inheritdoc />
    MemoryOwner<byte> IBinaryLogEntry.ToBuffer(MemoryAllocator<byte> allocator)
    {
        var buffer = allocator.AllocateExactly(T.Size);
        var writer = new SpanWriter<byte>(buffer.Span);
        Content.Format(ref writer);
        return buffer;
    }

    /// <inheritdoc />
    readonly ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => writer.WriteFormattableAsync(Content, token);
}