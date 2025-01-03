using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;

[SuppressMessage("Performance", "CA1812", Justification = "This class is instantiated by DI container")]
internal sealed class BackgroundCompactionService(PersistentState state) : BackgroundService
{
    private readonly Func<CancellationToken, ValueTask>? compaction = state switch
    {
        ILogCompactionSupport support => support.ForceCompactionAsync,
        MemoryBasedStateMachine { IsBackgroundCompaction: true } mbsm => mbsm.ForceIncrementalCompactionAsync,
        _ => null,
    };

    protected override async Task ExecuteAsync(CancellationToken token)
    {
        // fail fast if log is not configured for background compaction
        if (compaction is null)
            return;

        while (!token.IsCancellationRequested)
        {
            await state.WaitForCommitAsync(token).ConfigureAwait(false);
            await compaction(token).ConfigureAwait(false);
        }
    }
}