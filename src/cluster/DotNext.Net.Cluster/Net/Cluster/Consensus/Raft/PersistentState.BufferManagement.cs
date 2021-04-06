using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;

    public partial class PersistentState
    {
        private sealed class BufferingLogEntryConsumer : ILogEntryConsumer<IRaftLogEntry, (BufferedRaftLogEntryList, long?)>
        {
            private readonly RaftLogEntriesBufferingOptions options;

            internal BufferingLogEntryConsumer(RaftLogEntriesBufferingOptions options)
                => this.options = options;

            public async ValueTask<(BufferedRaftLogEntryList, long?)> ReadAsync<TEntry, TList>(TList entries, long? snapshotIndex, CancellationToken token)
                where TEntry : notnull, IRaftLogEntry
                where TList : notnull, IReadOnlyList<TEntry>
                => (await BufferedRaftLogEntryList.CopyAsync<TEntry, TList>(entries, options, token).ConfigureAwait(false), snapshotIndex);
        }

        private readonly ILogEntryConsumer<IRaftLogEntry, (BufferedRaftLogEntryList, long?)>? bufferingConsumer;
    }
}