using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Hosting;

namespace DotNext.Net.Cluster.Consensus.Raft;

using ILogCompactionSupport = IO.Log.ILogCompactionSupport;

[SuppressMessage("Performance", "CA1812", Justification = "This class is instantiated by DI container")]
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
        if (!state.IsBackgroundCompaction)
            return;

        while (!token.IsCancellationRequested)
        {
            await state.WaitForCommitAsync(token).ConfigureAwait(false);
            await compaction(token).ConfigureAwait(false);
        }
    }
}