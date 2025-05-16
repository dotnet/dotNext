using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using DotNext.IO;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

[Experimental("DOTNEXT001")]
internal sealed class SumStateMachine : SimpleStateMachine
{
    public long Value;
    
    public SumStateMachine(DirectoryInfo location)
        : base(new(Path.Combine(location.FullName, "db")))
    {
    }

    protected override async ValueTask RestoreAsync(FileInfo snapshotFile, CancellationToken token)
    {
        var bytes = await File.ReadAllBytesAsync(snapshotFile.FullName, token);
        Value = BinaryPrimitives.ReadInt64LittleEndian(bytes);
    }

    protected override ValueTask PersistAsync(IAsyncBinaryWriter writer, CancellationToken token)
    {
        var bytes = Span.AsBytes(ref Value).ToArray();
        return writer.Invoke(bytes, token);
    }

    protected override async ValueTask<bool> ApplyAsync(LogEntry entry, CancellationToken token)
    {
        byte[] array;
        if (entry.TryGetSequence(out var sequence))
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