using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using IO;

/// <summary>
/// Represents the log entry maintained by <see cref="WriteAheadLog"/> instance.
/// </summary>
[StructLayout(LayoutKind.Auto)]
[Experimental("DOTNEXT001")]
public readonly struct LogEntry : IInputLogEntry
{
    // ISnapshot, or byte[], or MemoryManager<byte>, or null
    private readonly object? payload;
    private readonly ReadOnlySequenceSegment<byte>? endSegment;
    private readonly int startIndex, endIndex;

    internal LogEntry(in LogEntryMetadata metadata, long index)
    {
        Index = index;
        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(metadata.Timestamp);
        CommandId = metadata.Id;
        Term = metadata.Term;
    }

    internal LogEntry(ReadOnlySequence<byte> sequence, in LogEntryMetadata metadata, long index)
        : this(in metadata, index)
    {
        if (SequenceMarshal.TryGetReadOnlySequenceSegment(sequence, out var startSegment, out startIndex, out endSegment, out endIndex))
        {
            payload = startSegment;
        }
        else if (SequenceMarshal.TryGetReadOnlyMemory(sequence, out var memory))
        {
            if (MemoryMarshal.TryGetArray(memory, out var segment))
            {
                startIndex = segment.Offset;
                endIndex = segment.Offset;
            }
            else if (MemoryMarshal.TryGetMemoryManager<byte, MemoryManager<byte>>(memory, out var manager, out startIndex, out endIndex))
            {
                payload = manager;
            }
        }
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

    public object? Context { get; init; }

    public long Index { get; }

    public long Term { get; }

    public DateTimeOffset Timestamp { get; }

    public int? CommandId { get; }

    long? IDataTransferObject.Length => TryGetSequence(out var sequence)
        ? sequence.Length
        : (payload as IDataTransferObject)?.Length;

    /// <summary>
    /// Gets a value indicating whether this log entry is a snapshot.
    /// </summary>
    public bool IsSnapshot => (payload as IRaftLogEntry)?.IsSnapshot ?? false;

    bool IDataTransferObject.IsReusable => payload is not IDataTransferObject dto || dto.IsReusable;

    public bool TryGetSequence(out ReadOnlySequence<byte> sequence)
    {
        switch (payload)
        {
            case byte[] array:
                sequence = new(new ReadOnlyMemory<byte>(array, startIndex, endIndex));
                break;
            case MemoryManager<byte> manager:
                sequence = new(manager.Memory.Slice(startIndex, endIndex));
                break;
            case ReadOnlySequenceSegment<byte> startSegment when endSegment is not null:
                sequence = new(startSegment, startIndex, endSegment, endIndex);
                break;
            default:
                sequence = ReadOnlySequence<byte>.Empty;
                return payload is null;
        }

        return true;
    }

    bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
    {
        switch (payload)
        {
            case byte[] array:
                memory = new(array, startIndex, endIndex);
                break;
            case MemoryManager<byte> manager:
                memory = manager.Memory.Slice(startIndex, endIndex);
                break;
            case IDataTransferObject snapshot:
                return snapshot.TryGetMemory(out memory);
            default:
                memory = ReadOnlyMemory<byte>.Empty;
                return payload is null;
        }

        return true;
    }

    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
    {
        return payload switch
        {
            byte[] array => writer.Invoke(new(array, startIndex, endIndex), token),
            MemoryManager<byte> manager => writer.Invoke(manager.Memory.Slice(startIndex, endIndex), token),
            ReadOnlySequenceSegment<byte> startSegment when endSegment is not null => WriteAsync(
                new(startSegment, startIndex, endSegment, endIndex), writer, token),
            IDataTransferObject snapshot => snapshot.WriteToAsync(writer, token),
            _ => ValueTask.CompletedTask
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