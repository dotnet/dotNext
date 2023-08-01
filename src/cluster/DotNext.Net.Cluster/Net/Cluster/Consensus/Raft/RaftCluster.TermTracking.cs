using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;

public partial class RaftCluster<TMember>
{
    private sealed class ReplicationWithSenderTermDetector<TEntry> : ILogEntryProducer<TEntry>
        where TEntry : notnull, IRaftLogEntry
    {
        private readonly ILogEntryProducer<TEntry> entries;
        private readonly long expectedTerm;
        private bool replicatedWithExpectedTerm;

        internal ReplicationWithSenderTermDetector(ILogEntryProducer<TEntry> entries, long expectedTerm)
        {
            Debug.Assert(entries is not null);

            this.entries = entries;
            this.expectedTerm = expectedTerm;
        }

        internal bool IsReplicatedWithExpectedTerm => replicatedWithExpectedTerm;

        TEntry IAsyncEnumerator<TEntry>.Current
        {
            get
            {
                var entry = entries.Current;
                replicatedWithExpectedTerm |= expectedTerm == entry.Term;
                return entry;
            }
        }

        long ILogEntryProducer<TEntry>.RemainingCount => entries.RemainingCount;

        ValueTask<bool> IAsyncEnumerator<TEntry>.MoveNextAsync() => entries.MoveNextAsync();

        ValueTask IAsyncDisposable.DisposeAsync() => entries.DisposeAsync();
    }
}