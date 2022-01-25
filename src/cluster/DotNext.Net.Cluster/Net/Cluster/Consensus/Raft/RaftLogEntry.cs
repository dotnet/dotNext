using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;
using IO.Log;

/// <summary>
/// Represents default implementation of <see cref="IRaftLogEntry"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct RaftLogEntry : IBinaryLogEntry
{
    private readonly ReadOnlyMemory<byte> content;

    /// <summary>
    /// Gets the timestamp of this log entry.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets Term value associated with this log entry.
    /// </summary>
    public long Term { get; init; }

    /// <summary>
    /// Gets the payload of the log entry.
    /// </summary>
    public ReadOnlyMemory<byte> Content
    {
        get => content;
        init => content = value;
    }

    /// <summary>
    /// Gets the command identifier.
    /// </summary>
    public int? CommandId { get; init; }

    /// <inheritdoc />
    bool ILogEntry.IsSnapshot => false;

    /// <inheritdoc />
    long? IDataTransferObject.Length => content.Length;

    /// <inheritdoc />
    bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc />
    bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
    {
        memory = content;
        return true;
    }

    /// <inheritdoc />
    MemoryOwner<byte> IBinaryLogEntry.ToBuffer(MemoryAllocator<byte> allocator)
        => content.Span.Copy(allocator);

    /// <inheritdoc />
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => writer.WriteAsync(content, lengthFormat: null, token);

    /// <inheritdoc />
    ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
        => transformation.TransformAsync(IAsyncBinaryReader.Create(content), token);
}