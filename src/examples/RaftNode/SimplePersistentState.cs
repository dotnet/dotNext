using DotNext;
using DotNext.Net.Cluster.Consensus.Raft;

namespace RaftNode;

internal sealed class SimplePersistentState(string path) : MemoryBasedStateMachine(path, 50, new Options { InitialPartitionSize = 50 * 8 }), ISupplier<long>
{
    internal const string LogLocation = "logLocation";

    private sealed class SimpleSnapshotBuilder(in SnapshotBuilderContext context) : IncrementalSnapshotBuilder(context)
    {
        private long value;

        protected override async ValueTask ApplyAsync(LogEntry entry)
            => value = await entry.GetReader().ReadLittleEndianAsync<long>().ConfigureAwait(false);

        public override ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => writer.WriteLittleEndianAsync(value, token);
    }

    private long content;

    public SimplePersistentState(IConfiguration configuration)
        : this(configuration[LogLocation] ?? string.Empty)
    {
    }

    long ISupplier<long>.Invoke() => Volatile.Read(in content);

    private async ValueTask UpdateValue(LogEntry entry)
    {
        var value = await entry.GetReader().ReadLittleEndianAsync<long>().ConfigureAwait(false);
        Volatile.Write(ref content, value);
        Console.WriteLine($"Accepting value {value}");
    }

    protected override ValueTask ApplyAsync(LogEntry entry)
        => entry.Length is 0L ? ValueTask.CompletedTask : UpdateValue(entry);

    protected override SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext context)
    {
        Console.WriteLine("Building snapshot");
        return new SimpleSnapshotBuilder(context);
    }
}