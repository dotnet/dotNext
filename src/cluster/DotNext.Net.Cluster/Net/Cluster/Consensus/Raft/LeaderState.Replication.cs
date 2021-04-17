using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;
    using Threading;
    using static Threading.Tasks.Synchronization;

    internal partial class LeaderState
    {
        private sealed class Replicator : TaskCompletionSource<Result<bool>>, ILogEntryConsumer<IRaftLogEntry, Result<bool>>
        {
            private readonly IRaftClusterMember member;
            private readonly long commitIndex, precedingIndex, precedingTerm, term;
            private readonly ILogger logger;
            private readonly CancellationToken token;

            // state
            private long currentIndex;
            private bool replicatedWithCurrentTerm;
            private ConfiguredTaskAwaitable<Result<bool>>.ConfiguredTaskAwaiter replicationAwaiter;

            internal Replicator(
                IRaftClusterMember member,
                long commitIndex,
                long currentIndex,
                long term,
                long precedingIndex,
                long precedingTerm,
                ILogger logger,
                CancellationToken token)
            {
                this.member = member;
                this.precedingIndex = precedingIndex;
                this.precedingTerm = precedingTerm;
                this.commitIndex = commitIndex;
                this.currentIndex = currentIndex;
                this.term = term;
                this.logger = logger;
                this.token = token;
            }

            internal ValueTask<Result<bool>> Start(IAuditTrail<IRaftLogEntry> auditTrail)
            {
                logger.ReplicationStarted(member.EndPoint, currentIndex);
                return currentIndex >= member.NextIndex ?
                    auditTrail.ReadAsync(this, member.NextIndex, token) :
                    ReadAsync<EmptyLogEntry, EmptyLogEntry[]>(Array.Empty<EmptyLogEntry>(), null, token);
            }

            private void Complete()
            {
                try
                {
                    var result = replicationAwaiter.GetResult();

                    // analyze result and decrease node index when it is out-of-sync with the current node
                    if (result.Value)
                    {
                        logger.ReplicationSuccessful(member.EndPoint, member.NextIndex);
                        member.NextIndex.VolatileWrite(currentIndex + 1);
                        result = result.SetValue(replicatedWithCurrentTerm);
                    }
                    else
                    {
                        logger.ReplicationFailed(member.EndPoint, member.NextIndex.UpdateAndGet(static index => index > 0L ? index - 1L : index));
                    }

                    SetResult(result);
                }
                catch (Exception e)
                {
                    SetException(e);
                }
                finally
                {
                    replicationAwaiter = default;
                }
            }

            public ValueTask<Result<bool>> ReadAsync<TEntry, TList>(TList entries, long? snapshotIndex, CancellationToken token)
                where TEntry : notnull, IRaftLogEntry
                where TList : notnull, IReadOnlyList<TEntry>
            {
                if (snapshotIndex.HasValue)
                {
                    logger.InstallingSnapshot(currentIndex = snapshotIndex.GetValueOrDefault());
                    replicationAwaiter = member.InstallSnapshotAsync(term, entries[0], currentIndex, token).ConfigureAwait(false).GetAwaiter();
                }
                else
                {
                    logger.ReplicaSize(member.EndPoint, entries.Count, precedingIndex, precedingTerm);
                    replicationAwaiter = member.AppendEntriesAsync<TEntry, TList>(term, entries, precedingIndex, precedingTerm, commitIndex, token).ConfigureAwait(false).GetAwaiter();
                }

                replicatedWithCurrentTerm = ContainsTerm(entries, term);
                if (replicationAwaiter.IsCompleted)
                    Complete();
                else
                    replicationAwaiter.OnCompleted(Complete);

                return new (Task);

                static bool ContainsTerm(TList list, long term)
                {
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (list[i].Term == term)
                            return true;
                    }

                    return false;
                }
            }
        }

        private sealed class WaitNode : TaskCompletionSource<bool>
        {
            public WaitNode()
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
            }
        }

        private volatile WaitNode replicationEvent, replicationQueue;

        private void DrainReplicationQueue()
            => Interlocked.Exchange(ref replicationQueue, new ()).SetResult(true);

        private Task<bool> WaitForReplicationAsync(TimeSpan period, CancellationToken token)
        {
            // This implementation optimized to avoid allocations of a new wait node.
            // The new node should be created when the current node is in signaled state.
            // Otherwise, we can keep the existing node
            var current = replicationEvent.Task;
            if (current.IsCompleted)
            {
                replicationEvent = new ();
            }
            else
            {
                current = current.WaitAsync(period, token);
            }

            return current;
        }

        internal Task<bool> ForceReplicationAsync(TimeSpan timeout, CancellationToken token)
        {
            var result = replicationQueue.Task;

            // resume heartbeat loop to force replication
            replicationEvent.TrySetResult(true);

            // enqueue a new task representing completion callback
            return result.WaitAsync(timeout, token);
        }
    }
}