using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;
using BitVector = Numerics.BitVector;

/// <summary>
/// Represents buffered log entry.
/// </summary>
/// <remarks>
/// This type is intended for developing transport-layer buffering
/// and low level I/O optimizations when writing custom Write-Ahead Log.
/// It's not recommended to use the type in the application code.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
[EditorBrowsable(EditorBrowsableState.Advanced)]
[SuppressMessage("Usage", "CA1001", Justification = "False positive")]
public readonly struct BufferedRaftLogEntry : IRaftLogEntry, IDisposable
{
    private const byte InMemoryFlag = 0x01;
    private const byte SnapshotFlag = InMemoryFlag << 1;
    private const byte IdentifierFlag = SnapshotFlag << 1;

    // possible values are:
    // null - empty content
    // FileStream - file
    // IGrowableBuffer<byte> - in-memory copy of the log entry
    private readonly IDisposable? content;
    private readonly int commandId;
    private readonly byte flags;

    private BufferedRaftLogEntry(string fileName, int bufferSize, long term, DateTimeOffset timestamp, int? id, bool snapshot)
    {
        Term = term;
        Timestamp = timestamp;
        flags = BitVector.FromBits<byte>([false, snapshot, id.HasValue]);
        commandId = id.GetValueOrDefault();
        content = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous | FileOptions.DeleteOnClose);
    }

    private BufferedRaftLogEntry(FileStream file, long term, DateTimeOffset timestamp, int? id, bool snapshot)
    {
        Term = term;
        Timestamp = timestamp;
        flags = BitVector.FromBits<byte>([false, snapshot, id.HasValue]);
        commandId = id.GetValueOrDefault();
        content = file;
    }

    private BufferedRaftLogEntry(IGrowableBuffer<byte> buffer, long term, DateTimeOffset timestamp, int? id, bool snapshot)
    {
        Term = term;
        Timestamp = timestamp;
        flags = BitVector.FromBits<byte>([true, snapshot, id.HasValue]);
        commandId = id.GetValueOrDefault();
        content = buffer;
    }

    private BufferedRaftLogEntry(long term, DateTimeOffset timestamp, int? id, bool snapshot)
    {
        Term = term;
        Timestamp = timestamp;
        flags = BitVector.FromBits<byte>([true, snapshot, id.HasValue]);
        commandId = id.GetValueOrDefault();
        content = null;
    }

    internal bool InMemory => (flags & InMemoryFlag) != 0U;

    /// <summary>
    /// Gets a value indicating whether the current log entry is a snapshot.
    /// </summary>
    public bool IsSnapshot => (flags & SnapshotFlag) != 0U;

    /// <summary>
    /// Gets date/time of when log entry was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets Term value associated with the log entry.
    /// </summary>
    public long Term { get; }

    /// <inheritdoc/>
    int? IRaftLogEntry.CommandId => (flags & IdentifierFlag) == 0U ? null : commandId;

    /// <summary>
    /// Gets length of this log entry, in bytes.
    /// </summary>
    public long Length => content switch
    {
        FileStream file => file.Length,
        IGrowableBuffer<byte> buffer => buffer.WrittenCount,
        _ => 0L
    };

    /// <inheritdoc/>
    long? IDataTransferObject.Length => Length;

    /// <inheritdoc/>
    bool IDataTransferObject.IsReusable => true;

    private static async ValueTask<BufferedRaftLogEntry> CopyToMemoryOrFileAsync<TEntry>(TEntry entry, RaftLogEntryBufferingOptions options, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        var writer = options.CreateBufferingWriter();
        var buffer = options.RentBuffer();
        try
        {
            await entry.WriteToAsync(writer, buffer.Memory, token).ConfigureAwait(false);
            await writer.FlushAsync(token).ConfigureAwait(false);
        }
        catch
        {
            await writer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            buffer.Dispose();
        }

        if (writer.TryGetWrittenContent(out _, out var fileName))
            return new(writer, entry.Term, entry.Timestamp, entry.CommandId, entry.IsSnapshot);

        await writer.DisposeAsync().ConfigureAwait(false);
        return new(fileName, options.BufferSize, entry.Term, entry.Timestamp, entry.CommandId, entry.IsSnapshot);
    }

    private static async ValueTask<BufferedRaftLogEntry> CopyToMemoryAsync<TEntry>(TEntry entry, int length, MemoryAllocator<byte>? allocator, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        var writer = new PooledBufferWriter<byte>(allocator) { Capacity = length };
        try
        {
            await entry.WriteToAsync(writer, token).ConfigureAwait(false);
        }
        catch
        {
            writer.Dispose();
            throw;
        }

        return new(writer, entry.Term, entry.Timestamp, entry.CommandId, entry.IsSnapshot);
    }

    internal static async ValueTask<BufferedRaftLogEntry> CopyToFileAsync<TEntry>(TEntry entry, RaftLogEntryBufferingOptions options, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        var output = new FileStream(options.GetRandomFileName(), new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            BufferSize = options.BufferSize,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose,
            PreallocationSize = entry.Length.GetValueOrDefault(),
        });

        var buffer = options.RentBuffer();
        try
        {
            if (entry.Length.TryGetValue(out var length))
                output.SetLength(length);

            await entry.WriteToAsync(output, buffer.Memory, token).ConfigureAwait(false);
            await output.FlushAsync(token).ConfigureAwait(false);
        }
        catch
        {
            await output.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            buffer.Dispose();
        }

        return new(output, entry.Term, entry.Timestamp, entry.CommandId, entry.IsSnapshot);
    }

    /// <summary>
    /// Constructs a copy of the specified log entry.
    /// </summary>
    /// <param name="entry">The log entry to be copied.</param>
    /// <param name="options">Buffering options.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TEntry">The type of the log entry to be copied.</typeparam>
    /// <returns>Buffered copy of <paramref name="entry"/>.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<BufferedRaftLogEntry> CopyAsync<TEntry>(TEntry entry, RaftLogEntryBufferingOptions options, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry
    {
        ValueTask<BufferedRaftLogEntry> result;
        if (!entry.Length.TryGetValue(out var length))
            result = CopyToMemoryOrFileAsync(entry, options, token);
        else if (length is 0L)
            result = new(new BufferedRaftLogEntry(entry.Term, entry.Timestamp, entry.CommandId, entry.IsSnapshot));
        else if (length <= options.MemoryThreshold)
            result = CopyToMemoryAsync(entry, (int)length, options.MemoryAllocator, token);
        else
            result = CopyToFileAsync(entry, options, token);

        return result;
    }

    /// <inheritdoc/>
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
    {
        ValueTask result;
        switch (content)
        {
            case FileStream fs:
                fs.Position = 0L;
                result = new(writer.CopyFromAsync(fs, token));
                break;
            case IGrowableBuffer<byte> buffer:
                result = buffer.CopyToAsync(writer, token);
                break;
            default:
                result = new();
                break;
        }

        return result;
    }

    /// <inheritdoc/>
    bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
    {
        switch (content)
        {
            case IGrowableBuffer<byte> buffer:
                return buffer.TryGetWrittenContent(out memory);
            case null:
                memory = ReadOnlyMemory<byte>.Empty;
                return true;
            default:
                memory = default;
                return false;
        }
    }

    /// <summary>
    /// Releases all resources associated with the buffer.
    /// </summary>
    public void Dispose() => content?.Dispose();
}