using System.Buffers;
using System.Buffers.Binary;
using DotNext.IO;
using DotNext.Net.Cluster.Consensus.Raft.StateMachine;

namespace RaftNode;

internal sealed class StateMachine(DirectoryInfo location) : SimpleStateMachine(location)
{
    internal const string LogLocation = "logLocation";
    
    internal long Value;

    public StateMachine(string location)
        : this(new DirectoryInfo(Path.GetFullPath(Path.Combine(location, "db"))))
    {
    }

    public StateMachine(IConfiguration config)
        : this(config[LogLocation] ?? string.Empty)
    {
    }

    protected override async ValueTask RestoreAsync(FileInfo snapshotFile, CancellationToken token)
    {
        var handle = File.OpenHandle(snapshotFile.FullName, options: FileOptions.Asynchronous);
        var buffer = new byte[sizeof(long)];
        await RandomAccess.ReadAsync(handle, buffer, fileOffset: 0L, token).ConfigureAwait(false);
        Value = BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    protected override ValueTask PersistAsync(IAsyncBinaryWriter writer, CancellationToken token)
    {
        Console.WriteLine("Building snapshot");
        var buffer = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, Value);
        return writer.Invoke(buffer, token);
    }

    protected override ValueTask<bool> ApplyAsync(LogEntry entry, CancellationToken token)
    {
        if (entry.TryGetPayload(out var payload))
        {
            var value = ToInt64(payload);
            Console.WriteLine($"Accepting value {value}");
            Value = value;
        }
        else
        {
            Console.WriteLine("Unexpected payload");
            throw new Exception("Invalid log entry");
        }

        return new(entry.Index % 10L is 0); // create snapshot every 10 log entries

        static long ToInt64(in ReadOnlySequence<byte> sequence)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            sequence.CopyTo(buffer);
            return BinaryPrimitives.ReadInt64LittleEndian(buffer);
        }
    }
}