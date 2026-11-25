using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Collections.Concurrent;
using ReplicationUtils;
using Threading;
using Timestamp = Diagnostics.Timestamp;

internal partial class LeaderState<TMember>
{
    private readonly AsyncAutoResetEvent replicationEvent;
    private readonly BoundedObjectPool<ReplicationBarrier> barriers;

    [SuppressMessage("Usage", "CA2213", Justification = "Disposed correctly by Dispose() method")]
    private readonly SingleProducerMultipleConsumersCoordinator replicationQueue;

    private ValueTask<bool> WaitForReplicationAsync(Timestamp startTime, TimeSpan period, CancellationToken token)
    {
        // subtract heartbeat processing duration from heartbeat period for better stability
        return replicationEvent.WaitAsync(TimeSpan.Max(period - startTime.Elapsed, TimeSpan.Zero), token);
    }

    internal ValueTask ForceReplicationAsync(CancellationToken token)
    {
        // enqueue a new task representing completion callback
        var task = replicationQueue.WaitAsync(token);
        if (task.IsCompleted)
        {
            task = task.CompleteSynchronously();
        }
        else
        {
            try
            {
                // resume heartbeat loop to force replication
                replicationEvent.Set();
            }
            catch (ObjectDisposedException e)
            {
                task = ValueTask.FromException(new NotLeaderException(e));
            }
        }

        return task;
    }

    // Waits for the completion of the first round started after the stamp has been captured
    internal ValueTask WaitForRoundAsync(long stamp, CancellationToken token)
    {
        var task = replicationQueue.WaitAsync(stamp, token);
        return task.IsCompleted ? task.CompleteSynchronously() : task;
    }

    internal long Stamp => replicationQueue.Stamp;

    // synchronous version that doesn't wait for the end of replication round
    internal void ForceReplication()
    {
        try
        {
            replicationEvent.Set();
        }
        catch (ObjectDisposedException e)
        {
            throw new NotLeaderException(e);
        }
    }

    private ReplicationBarrier RentBarrier() => barriers.TryGet() ?? new PoolingReplicationBarrier(barriers);
}

file sealed class PoolingReplicationBarrier(BoundedObjectPool<ReplicationBarrier> pool) : ReplicationBarrier
{
    protected override void ReuseCore() => pool.TryReturn(this);
}

file static class ValueTaskExtensions
{
    public static ValueTask CompleteSynchronously(this in ValueTask task)
    {
        try
        {
            task.GetAwaiter().GetResult();
            return ValueTask.CompletedTask;
        }
        catch (ObjectDisposedException e)
        {
            return ValueTask.FromException(new NotLeaderException(e));
        }
        catch (OperationCanceledException e)
        {
            return ValueTask.FromCanceled(e.CancellationToken);
        }
        catch (Exception e)
        {
            return ValueTask.FromException(e);
        }
    }
}