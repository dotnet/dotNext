using System.Buffers;
using System.Buffers.Binary;
using DotNext.IO;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

internal sealed class SumStateMachine(DirectoryInfo location) : SimpleStateMachine(new(Path.Combine(location.FullName, "db")))
{
    public long Value;

    protected override async ValueTask RestoreAsync(FileInfo snapshotFile, CancellationToken token)
    {
        var bytes = await File.ReadAllBytesAsync(snapshotFile.FullName, token);
        Value = BinaryPrimitives.ReadInt64LittleEndian(bytes);
    }

    protected override ValueTask PersistAsync(IAsyncBinaryWriter writer, CancellationToken token)
    {
        ReadOnlyMemory<byte> bytes = long.AsReadOnlyBytes(in Value).ToArray();
        return writer.Invoke(bytes, token);
    }

    protected override async ValueTask<bool> ApplyAsync(LogEntry entry, CancellationToken token)
    {
        byte[] array;
        if (entry.TryGetPayload(out var sequence))
        {
            array = sequence.ToArray();
        }
        else
        {
            array = await entry.ToByteArrayAsync(token: token);
        }

        Value += BinaryPrimitives.ReadInt64LittleEndian(array);
        return entry.Index % 5 is 0;
    }
}