using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO;
using Text.Json;

/// <summary>
/// Represents JSON-serializable log entry.
/// </summary>
/// <typeparam name="T">JSON-serializable type.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct JsonLogEntry<T>() : IRaftLogEntry
    where T : notnull, IJsonSerializable<T>
{
    /// <summary>
    /// Gets the payload of this log entry.
    /// </summary>
    required public T? Content { get; init; }

    /// <summary>
    /// Gets Term value associated with this log entry.
    /// </summary>
    required public long Term { get; init; }

    /// <summary>
    /// Gets the timestamp of this log entry.
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <inheritdoc />
    long? IDataTransferObject.Length => null;

    /// <inheritdoc />
    bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc />
    int? IRaftLogEntry.CommandId => null;

    /// <inheritdoc />
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => JsonSerializable<T>.SerializeAsync(writer, Content, token);
}