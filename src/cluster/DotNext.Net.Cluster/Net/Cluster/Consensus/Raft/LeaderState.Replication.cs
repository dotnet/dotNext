using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Membership;
using Threading;

internal partial class LeaderState<TMember>
{
    internal sealed class Replicator : TaskCompletionSource<Result<bool>>, ILogEntryConsumer<IRaftLogEntry, Result<bool>>
    {
        private readonly IAuditTrail<IRaftLogEntry> auditTrail;
        private readonly IClusterConfiguration activeConfig;
        private readonly IClusterConfiguration? proposedConfig;
        internal readonly TMember Member;
        private readonly long commitIndex, precedingIndex, precedingTerm, term;
        private readonly ILogger logger;
        private readonly CancellationToken token;

        // state
        private long replicationIndex, fingerprint;
        private bool replicatedWithCurrentTerm;
        private ConfiguredTaskAwaitable<Result<bool>>.ConfiguredTaskAwaiter replicationAwaiter;

        // TODO: Replace with required init properties in the next version of C#
        internal Replicator(
            IAuditTrail<IRaftLogEntry> auditTrail,
            IClusterConfiguration activeConfig,
            IClusterConfiguration? proposedConfig,
            TMember member,
            long commitIndex,
            long term,
            long precedingIndex,
            long precedingTerm,
            ILogger logger,
            CancellationToken token)
        {
            this.auditTrail = auditTrail;
            this.activeConfig = activeConfig;
            this.proposedConfig = proposedConfig;
            Member = member;
            this.precedingIndex = precedingIndex;
            this.precedingTerm = precedingTerm;
            this.commitIndex = commitIndex;
            this.term = term;
            this.logger = logger;
            this.token = token;
            fingerprint = (proposedConfig ?? activeConfig).Fingerprint;
        }

        internal ValueTask<Result<bool>> ReplicateAsync(long currentIndex)
        {
            var startIndex = precedingIndex + 1L;
            Debug.Assert(startIndex == Member.NextIndex);

            logger.ReplicationStarted(Member.EndPoint, startIndex);
            return currentIndex >= startIndex ?
                auditTrail.ReadAsync(this, startIndex, token) :
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
                    logger.ReplicationSuccessful(Member.EndPoint, Member.NextIndex);
                    Member.NextIndex = replicationIndex + 1L;
                    Member.ConfigurationFingerprint = fingerprint;
                    result = result with { Value = replicatedWithCurrentTerm };
                }
                else
                {
                    Member.ConfigurationFingerprint = 0L;
                    var nextIndex = Member.NextIndex;
                    if (nextIndex > 0L)
                        Member.NextIndex = --nextIndex;

                    logger.ReplicationFailed(Member.EndPoint, nextIndex);
                }

                SetResult(result);
            }
            catch (OperationCanceledException e)
            {
                SetCanceled(e.CancellationToken);
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

        private (IClusterConfiguration, bool) GetConfiguration()
        {
            bool applyConfig;
            IClusterConfiguration configuration;

            if (Member.ConfigurationFingerprint == fingerprint)
            {
                applyConfig = activeConfig.Fingerprint == fingerprint;
                configuration = IClusterConfiguration.CreateEmpty(fingerprint);
            }
            else
            {
                applyConfig = false;
                configuration = proposedConfig ?? activeConfig;
            }

            return (configuration, applyConfig);
        }

        public ValueTask<Result<bool>> ReadAsync<TEntry, TList>(TList entries, long? snapshotIndex, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
            where TList : notnull, IReadOnlyList<TEntry>
        {
            if (snapshotIndex.HasValue)
            {
                logger.InstallingSnapshot(replicationIndex = snapshotIndex.GetValueOrDefault());
                fingerprint = 0L;
                replicationAwaiter = Member.InstallSnapshotAsync(term, entries[0], replicationIndex, token).ConfigureAwait(false).GetAwaiter();
            }
            else
            {
                logger.ReplicaSize(Member.EndPoint, entries.Count, precedingIndex, precedingTerm);
                var (config, applyConfig) = GetConfiguration();
                replicationAwaiter = Member.AppendEntriesAsync<TEntry, TList>(term, entries, precedingIndex, precedingTerm, commitIndex, config, applyConfig, token).ConfigureAwait(false).GetAwaiter();
                replicationIndex = precedingIndex + entries.Count;
            }

            replicatedWithCurrentTerm = ContainsTerm(entries, term);
            if (replicationAwaiter.IsCompleted)
                Complete();
            else
                replicationAwaiter.OnCompleted(Complete);

            return new(Task);

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

    private sealed class ReplicationWorkItem : TaskCompletionSource<Result<bool>>, IThreadPoolWorkItem
    {
        private readonly long currentIndex;
        private ConfiguredValueTaskAwaitable<Result<bool>>.ConfiguredValueTaskAwaiter awaiter;

        internal ReplicationWorkItem(Replicator replicator, long currentIndex)
            : base(replicator, TaskCreationOptions.RunContinuationsAsynchronously)
        {
            Debug.Assert(replicator is not null);

            this.currentIndex = currentIndex;
        }

        private Replicator AsyncState
        {
            get
            {
                Debug.Assert(Task.AsyncState is Replicator);

                return Unsafe.As<Replicator>(Task.AsyncState);
            }
        }

        internal static TMember? GetReplicatedMember(Task<Result<bool>> task)
            => (task.AsyncState as Replicator)?.Member;

        private void OnCompleted() => Complete(ref awaiter);

        void IThreadPoolWorkItem.Execute()
        {
            var awaiter = AsyncState.ReplicateAsync(currentIndex).ConfigureAwait(false).GetAwaiter();

            if (awaiter.IsCompleted)
            {
                Complete(ref awaiter);
            }
            else
            {
                this.awaiter = awaiter;
                awaiter.UnsafeOnCompleted(OnCompleted);
            }
        }

        private void Complete(ref ConfiguredValueTaskAwaitable<Result<bool>>.ConfiguredValueTaskAwaiter awaiter)
        {
            try
            {
                SetResult(awaiter.GetResult());
            }
            catch (OperationCanceledException e)
            {
                SetCanceled(e.CancellationToken);
            }
            catch (Exception e)
            {
                SetException(e);
            }
            finally
            {
                awaiter = default; // help GC
            }
        }
    }

    private sealed class ReplicationCallback : TaskCompletionSource
    {
        private ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter parent;

        internal ReplicationCallback(in ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter parent)
            => this.parent = parent;

        internal void Invoke()
        {
            Debug.Assert(parent.IsCompleted);

            try
            {
                parent.GetResult();
                TrySetResult();
            }
            catch (ObjectDisposedException e)
            {
                TrySetException(new InvalidOperationException(ExceptionMessages.LocalNodeNotLeader, e));
            }
            catch (OperationCanceledException e)
            {
                TrySetCanceled(e.CancellationToken);
            }
            catch (Exception e)
            {
                TrySetException(e);
            }
            finally
            {
                parent = default; // help GC
            }
        }
    }

    private readonly AsyncAutoResetEvent replicationEvent = new(initialState: false);

    // We're using AsyncTrigger instead of TaskCompletionSource because adding a new completion
    // callback to AsyncTrigger is always O(1) in contrast to TaskCompletionSource which provides
    // O(n) as worst case (underlying list of completion callbacks organized as List<T>
    // in combination with monitor lock for insertion)
    [SuppressMessage("Usage", "CA2213", Justification = "Disposed correctly by Dispose() method")]
    private readonly AsyncTrigger replicationQueue = new();

    private void DrainReplicationQueue()
        => replicationQueue.Signal(resumeAll: true);

    private ValueTask<bool> WaitForReplicationAsync(TimeSpan period, CancellationToken token)
        => replicationEvent.WaitAsync(period, token);

    internal Task ForceReplicationAsync(CancellationToken token)
    {
        Task result;
        try
        {
            // enqueue a new task representing completion callback
            var replicationTask = replicationQueue.WaitAsync(token).ConfigureAwait(false).GetAwaiter();

            // resume heartbeat loop to force replication
            replicationEvent.Set();

            if (replicationTask.IsCompleted)
            {
                replicationTask.GetResult();
                result = Task.CompletedTask;
            }
            else
            {
                var callback = new ReplicationCallback(replicationTask);
                replicationTask.UnsafeOnCompleted(callback.Invoke);
                result = callback.Task;
            }
        }
        catch (ObjectDisposedException e)
        {
            result = Task.FromException(new InvalidOperationException(ExceptionMessages.LocalNodeNotLeader, e));
        }
        catch (OperationCanceledException e)
        {
            result = Task.FromCanceled(e.CancellationToken);
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }

        return result;
    }

    private static Task<Result<bool>> QueueReplication(Replicator replicator, long currentIndex)
    {
        var workItem = new ReplicationWorkItem(replicator, currentIndex);
        ThreadPool.UnsafeQueueUserWorkItem(workItem, preferLocal: false);
        return workItem.Task;
    }
}