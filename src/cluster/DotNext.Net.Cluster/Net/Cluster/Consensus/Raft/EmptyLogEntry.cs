using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;
using IO.Log;
using StateMachine;

/// <summary>
/// Represents No-OP entry.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct EmptyLogEntry() : ISupplier<MemoryAllocator<byte>, MemoryOwner<byte>>, IBinaryLogEntry, ISnapshot
{
    /// <inheritdoc/>
    int? IRaftLogEntry.CommandId => null;

    /// <inheritdoc cref="ILogEntry.IsSnapshot"/>
    public bool IsSnapshot { get; internal init; }

    /// <inheritdoc/>
    long? IDataTransferObject.Length => 0L;

    /// <inheritdoc/>
    int IBinaryLogEntry.Length => 0;

    /// <inheritdoc/>
    bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc/>
    bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
    {
        memory = ReadOnlyMemory<byte>.Empty;
        return true;
    }

    /// <summary>
    /// Gets or sets log entry term.
    /// </summary>
    required public long Term { get; init; }

    /// <summary>
    /// Gets timestamp of this log entry.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <inheritdoc/>
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => ValueTask.CompletedTask;

    /// <inheritdoc/>
    ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
        => IDataTransferObject.Empty.TransformAsync<TResult, TTransformation>(transformation, token);

    /// <inheritdoc/>
    MemoryOwner<byte> ISupplier<MemoryAllocator<byte>, MemoryOwner<byte>>.Invoke(MemoryAllocator<byte> allocator)
        => default;

    /// <inheritdoc/>
    void IBinaryLogEntry.WriteTo(Span<byte> buffer)
    {
    }

    /// <inheritdoc/>
    long ISnapshot.Index => 0L;
}