using DotNext.IO;
using DotNext.Net.Cluster.Consensus.Raft;
using static DotNext.Threading.AtomicInt64;

namespace RaftNode;

internal sealed class SimplePersistentState : PersistentState, IValueProvider
{
    internal const string LogLocation = "logLocation";

    private sealed class SimpleSnapshotBuilder : IncrementalSnapshotBuilder
    {
        private long value;

        public SimpleSnapshotBuilder(in SnapshotBuilderContext context)
            : base(context)
        {
        }

        protected override async ValueTask ApplyAsync(LogEntry entry)
            => value = await entry.ToTypeAsync<long, LogEntry>().ConfigureAwait(false);

        public override ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => writer.WriteAsync(value, token);
    }

    private long content;

    public SimplePersistentState(string path, AppEventSource source)
        : base(path, 50, CreateOptions(source))
    {
    }

    public SimplePersistentState(IConfiguration configuration, AppEventSource source)
        : this(configuration[LogLocation], source)
    {
    }

    private static Options CreateOptions(AppEventSource source)
    {
        var result = new Options
        {
            InitialPartitionSize = 50 * 8,
            WriteCounter = new("WAL.Writes", source),
            ReadCounter = new("WAL.Reads", source),
            CommitCounter = new("WAL.Commits", source),
            CompactionCounter = new("WAL.Compaction", source),
            LockContentionCounter = new("WAL.LockContention", source),
            LockDurationCounter = new("WAL.LockDuration", source),
        };

        result.WriteCounter.DisplayUnits =
            result.ReadCounter.DisplayUnits =
            result.CommitCounter.DisplayUnits =
            result.CompactionCounter.DisplayUnits = "entries";

        result.LockDurationCounter.DisplayUnits = "milliseconds";
        result.LockDurationCounter.DisplayName = "WAL Lock Duration";

        result.LockContentionCounter.DisplayName = "Lock Contention";

        result.WriteCounter.DisplayName = "Number of written entries";
        result.ReadCounter.DisplayName = "Number of retrieved entries";
        result.CommitCounter.DisplayName = "Number of committed entries";
        result.CompactionCounter.DisplayName = "Number of squashed entries";

        return result;
    }

    long IValueProvider.Value => content.VolatileRead();

    private async ValueTask UpdateValue(LogEntry entry)
    {
        var value = await entry.ToTypeAsync<long, LogEntry>().ConfigureAwait(false);
        content.VolatileWrite(value);
        Console.WriteLine($"Accepting value {value}");
    }

    protected override ValueTask ApplyAsync(LogEntry entry)
        => entry.Length == 0L ? new ValueTask() : UpdateValue(entry);

    async Task IValueProvider.UpdateValueAsync(long value, TimeSpan timeout, CancellationToken token)
    {
        var commitIndex = GetLastIndex(true);
        await AppendAsync(new Int64LogEntry { Content = value, Term = Term }, token);
        await WaitForCommitAsync(commitIndex + 1L, timeout, token);
    }

    protected override SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext context)
    {
        Console.WriteLine("Building snapshot");
        return new SimpleSnapshotBuilder(context);
    }
}