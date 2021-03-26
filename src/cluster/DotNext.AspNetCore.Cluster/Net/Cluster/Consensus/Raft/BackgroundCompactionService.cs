using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using ILogCompactionSupport = IO.Log.ILogCompactionSupport;

    internal sealed class BackgroundCompactionService : BackgroundService
    {
        private readonly PersistentState state;
        private readonly Func<CancellationToken, ValueTask> compaction;

        public BackgroundCompactionService(PersistentState state)
        {
            this.state = state;
            compaction = state is ILogCompactionSupport support ?
                support.ForceCompactionAsync :
                state.ForceIncrementalCompactionAsync;
        }

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            // fail fast if log is not configured for background compaction
            if (state.Compaction != PersistentState.CompactionMode.Background)
                return;

            while (!token.IsCancellationRequested)
            {
                await state.WaitForCommitAsync(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
                await compaction(token).ConfigureAwait(false);
            }
        }
    }
}
