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
        ValueTask replicationTask;
        try
        {
            // enqueue a new task representing completion callback
            replicationTask = replicationQueue.WaitAsync(token);

            // resume heartbeat loop to force replication
            replicationEvent.Set();
        }
        catch (ObjectDisposedException e)
        {
            replicationTask = ValueTask.FromException(new NotLeaderException(e));
        }

        return replicationTask;
    }

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