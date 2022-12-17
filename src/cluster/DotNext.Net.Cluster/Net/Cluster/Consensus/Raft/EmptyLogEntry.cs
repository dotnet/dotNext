using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO;
using IO.Log;

/// <summary>
/// Represents No-OP entry.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct EmptyLogEntry : IRaftLogEntry
{
    /// <summary>
    /// Initializes a new empty log entry.
    /// </summary>
    /// <param name="term">The term value.</param>
    public EmptyLogEntry(long term)
    {
        Term = term;
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc/>
    int? IRaftLogEntry.CommandId => null;

    /// <inheritdoc cref="ILogEntry.IsSnapshot"/>
    public bool IsSnapshot
    {
        get;
        internal init;
    }

    /// <inheritdoc/>
    long? IDataTransferObject.Length => 0L;

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
    public long Term { get; }

    /// <summary>
    /// Gets timestamp of this log entry.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <inheritdoc/>
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => ValueTask.CompletedTask;

    /// <inheritdoc/>
    ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
        => IDataTransferObject.Empty.TransformAsync<TResult, TTransformation>(transformation, token);
}