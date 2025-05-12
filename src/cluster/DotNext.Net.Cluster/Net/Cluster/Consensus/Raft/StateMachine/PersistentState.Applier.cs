using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Runtime.CompilerServices;
using Threading;

partial class PersistentState
{
    private readonly ChannelWriter<long> appendTrigger;
    private readonly Task appenderTask;
    private readonly AsyncTrigger applyEvent;
    private long appliedIndex; // volatile

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task ApplyAsync(ChannelReader<long> reader)
    {
        var token = reader.Completion.AsCancellationToken();
        for (var previousIndex = 1L; await reader.WaitToReadAsync().ConfigureAwait(false);)
        {
            for (long newIndex; reader.TryRead(out newIndex) && newIndex > previousIndex; previousIndex = newIndex)
            {
                // Ensure that the appender is not running with the snapshot installation process concurrently
                await lockManager.AcquireReadLockAsync(token).ConfigureAwait(false);
                try
                {
                    newIndex = await ApplyAsync(previousIndex, newIndex, token).ConfigureAwait(false);
                }
                finally
                {
                    lockManager.ReleaseReadLock();
                }

                if (previousIndex < newIndex)
                {
                    Volatile.Write(ref appliedIndex, newIndex);
                    applyEvent.Signal(resumeAll: true);
                    await cleanupTrigger.WriteAsync(newIndex, token).ConfigureAwait(false);
                }
            }
        }
    }

    // returns snapshot index
    private async ValueTask<long> ApplyAsync(long fromIndex, long toIndex, CancellationToken token)
    {
        return toIndex;
    }
}