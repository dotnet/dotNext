using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using IO;
using IO.Log;

/// <summary>
/// Represents the log entry maintained by <see cref="WriteAheadLog"/> instance.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[Experimental("DOTNEXT001")]
public readonly struct LogEntry : IInputLogEntry
{
    // ISnapshot, or IMemoryReader, or null
    private readonly object? payload;
    private readonly ulong address;
    private readonly long length;

    internal LogEntry(in LogEntryMetadata metadata, long index, IMemoryView? reader)
    {
        Index = index;
        Timestamp = new(metadata.Timestamp, TimeSpan.Zero);
        CommandId = metadata.Id;
        Term = metadata.Term;
        payload = reader;
        address = metadata.Offset;
        length = metadata.Length;
    }

    internal LogEntry(IRaftLogEntry snapshot, long index)
    {
        payload = snapshot;
        Index = index;
        Timestamp = snapshot.Timestamp;
        CommandId = snapshot.CommandId;
        Term = snapshot.Term;
    }

    internal LogEntry(ISnapshot snapshot)
        : this(snapshot, snapshot.Index)
    {
    }

    /// <inheritdoc cref="IInputLogEntry.Context"/>
    public object? Context { get; init; }

    /// <summary>
    /// Gets the index of this log entry.
    /// </summary>
    public long Index { get; }

    /// <summary>
    /// Gets the term of this log entry.
    /// </summary>
    public long Term { get; }

    /// <inheritdoc cref="ILogEntry.Timestamp"/>
    public DateTimeOffset Timestamp { get; }

    /// <inheritdoc cref="IRaftLogEntry.CommandId"/>
    public int? CommandId { get; }

    /// <inheritdoc/>
    public long? Length => payload is IDataTransferObject dto ? dto.Length : length;

    /// <summary>
    /// Gets a value indicating whether this log entry is a snapshot.
    /// </summary>
    public bool IsSnapshot => payload is ILogEntry { IsSnapshot: true };

    /// <inheritdoc/>
    bool IDataTransferObject.IsReusable => payload is not IDataTransferObject dto || dto.IsReusable;

    /// <summary>
    /// Attempts to get log entry payload.
    /// </summary>
    /// <param name="sequence">A sequence of bytes representing the log entry payload.</param>
    /// <returns><see langword="true"/> if this log entry is not a snapshot; otherwise, <see langword="false"/>.</returns>
    public bool TryGetPayload(out ReadOnlySequence<byte> sequence)
    {
        if (payload is IMemoryView view)
        {
            sequence = view.GetSequence(address, length);
            return true;
        }

        sequence = ReadOnlySequence<byte>.Empty;
        return payload is null;
    }

    /// <summary>
    /// Gets the payload of this log entry in the form of a memory block sequence.
    /// </summary>
    /// <returns>A collection of the memory blocks; or empty collection if <see cref="IsSnapshot"/> is <see langword="true"/>.</returns>
    public IEnumerable<ReadOnlyMemory<byte>> GetPayload()
        => (payload as IMemoryView)?.EnumerateMemoryBlocks(address, length) ?? [];

    /// <inheritdoc/>
    bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
    {
        switch (payload)
        {
            case IMemoryView view:
                return view.TryGetMemory(address, length, out memory);
            case IDataTransferObject snapshot:
                return snapshot.TryGetMemory(out memory);
            default:
                memory = ReadOnlyMemory<byte>.Empty;
                return payload is null;
        }
    }

    /// <inheritdoc/>
    public ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        where TWriter : IAsyncBinaryWriter
    {
        return payload switch
        {
            IMemoryView reader => reader.TryGetMemory(address, length, out var memory)
                ? writer.Invoke(memory, token)
                : WriteAsync(reader.GetSequence(address, length), writer, token),
            IDataTransferObject dto => dto.WriteToAsync(writer, token),
            _ => ValueTask.CompletedTask,
        };

        static async ValueTask WriteAsync(ReadOnlySequence<byte> sequence, TWriter writer, CancellationToken token)
        {
            foreach (var segment in sequence)
            {
                await writer.Invoke(segment, token).ConfigureAwait(false);
            }
        }
    }
}