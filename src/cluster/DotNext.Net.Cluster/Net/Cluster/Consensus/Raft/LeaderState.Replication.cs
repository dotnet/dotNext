using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Membership;
using Threading;
using IDataTransferObject = IO.IDataTransferObject;

internal partial class LeaderState<TMember>
{
    internal sealed class Replicator : TaskCompletionSource<Result<bool>>, ILogEntryConsumer<IRaftLogEntry, Result<bool>>, IClusterConfiguration
    {
        private readonly IClusterConfiguration configuration;
        internal readonly TMember Member;
        private readonly long commitIndex, precedingTerm, term;
        private readonly ILogger logger;

        // state
        private long replicationIndex; // reuse as precedingIndex
        private long fingerprint;
        private bool replicatedWithCurrentTerm; // reuse as applyConfig
        private ConfiguredTaskAwaitable<Result<bool>>.ConfiguredTaskAwaiter replicationAwaiter;

        // TODO: Replace with required init properties in the next version of C#
        internal Replicator(
            IClusterConfiguration activeConfig,
            IClusterConfiguration? proposedConfig,
            TMember member,
            long commitIndex,
            long term,
            long precedingIndex,
            long precedingTerm,
            ILogger logger)
        {
            Member = member;
            replicationIndex = precedingIndex;
            this.precedingTerm = precedingTerm;
            this.commitIndex = commitIndex;
            this.term = term;
            this.logger = logger;

            configuration = proposedConfig ?? activeConfig;
            fingerprint = configuration.Fingerprint;

            if (member.ConfigurationFingerprint == fingerprint)
            {
                // This branch is a hot path because configuration changes rarely.
                // It is reasonable to prevent allocation of empty configuration every time.
                // To do that, instance of Replicator serves as empty configuration
                replicatedWithCurrentTerm = activeConfig.Fingerprint == fingerprint;
                configuration = this;
            }
        }

        internal ValueTask<Result<bool>> ReplicateAsync(IAuditTrail<IRaftLogEntry> auditTrail, long currentIndex, CancellationToken token)
        {
            var startIndex = replicationIndex + 1L;
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

        public ValueTask<Result<bool>> ReadAsync<TEntry, TList>(TList entries, long? snapshotIndex, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
            where TList : notnull, IReadOnlyList<TEntry>
        {
            if (snapshotIndex.HasValue)
            {
                ReplicateSnapshot(entries[0], snapshotIndex.GetValueOrDefault(), token);
            }
            else
            {
                ReplicateEntries<TEntry, TList>(entries, token);
            }

            if (replicationAwaiter.IsCompleted)
                Complete();
            else
                replicationAwaiter.OnCompleted(Complete);

            return new(Task);
        }

        private void ReplicateSnapshot<TSnapshot>(TSnapshot snapshot, long snapshotIndex, CancellationToken token)
            where TSnapshot : notnull, IRaftLogEntry
        {
            Debug.Assert(snapshot.IsSnapshot);

            logger.InstallingSnapshot(replicationIndex = snapshotIndex);
            replicationAwaiter = Member.InstallSnapshotAsync(term, snapshot, snapshotIndex, token).ConfigureAwait(false).GetAwaiter();
            fingerprint = Member.ConfigurationFingerprint; // keep local version unchanged
            replicatedWithCurrentTerm = snapshot.Term == term;
        }

        private void ReplicateEntries<TEntry, TList>(TList entries, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
            where TList : notnull, IReadOnlyList<TEntry>
        {
            logger.ReplicaSize(Member.EndPoint, entries.Count, replicationIndex, precedingTerm, Member.ConfigurationFingerprint, fingerprint, replicatedWithCurrentTerm);
            replicationAwaiter = Member.AppendEntriesAsync<TEntry, TList>(term, entries, replicationIndex, precedingTerm, commitIndex, configuration, replicatedWithCurrentTerm, token).ConfigureAwait(false).GetAwaiter();
            replicationIndex += entries.Count;

            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].Term == term)
                {
                    replicatedWithCurrentTerm = true;
                    return;
                }
            }

            replicatedWithCurrentTerm = false;
        }

        long IClusterConfiguration.Fingerprint => fingerprint;

        long IClusterConfiguration.Length => 0L;

        bool IDataTransferObject.IsReusable => true;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => IDataTransferObject.Empty.WriteToAsync(writer, token);

        bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
            => IDataTransferObject.Empty.TryGetMemory(out memory);
    }

    private sealed class ReplicationWorkItem : TaskCompletionSource<Result<bool>>, IThreadPoolWorkItem
    {
        private readonly long currentIndex;
        private CancellationToken token;
        private IAuditTrail<IRaftLogEntry>? auditTrail;
        private Replicator? replicator;
        private ConfiguredValueTaskAwaitable<Result<bool>>.ConfiguredValueTaskAwaiter awaiter;

        internal ReplicationWorkItem(Replicator replicator, IAuditTrail<IRaftLogEntry> auditTrail, long currentIndex, CancellationToken token)
            : base(replicator.Member, TaskCreationOptions.RunContinuationsAsynchronously)
        {
            Debug.Assert(replicator is not null);
            Debug.Assert(auditTrail is not null);

            this.currentIndex = currentIndex;
            this.auditTrail = auditTrail;
            this.token = token;
            this.replicator = replicator;
        }

        internal static TMember GetReplicatedMember(Task<Result<bool>> task)
        {
            Debug.Assert(task is { IsCompleted: true, AsyncState: TMember });

            return (TMember)task.AsyncState;
        }

        private void OnCompleted() => Complete(ref awaiter);

        void IThreadPoolWorkItem.Execute()
        {
            var replicator = this.replicator;
            var auditTrail = this.auditTrail;
            var token = this.token;

            Debug.Assert(replicator is not null);
            Debug.Assert(auditTrail is not null);

            // help GC
            this.replicator = null;
            this.auditTrail = null;
            this.token = default;

            var awaiter = replicator.ReplicateAsync(auditTrail, currentIndex, token).ConfigureAwait(false).GetAwaiter();

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
            var replicationTask = replicationQueue.WaitAsync(token);

            // resume heartbeat loop to force replication
            replicationEvent.Set();

            if (replicationTask.IsCompleted)
            {
                replicationTask.GetAwaiter().GetResult();
                result = Task.CompletedTask;
            }
            else
            {
                result = replicationTask.AsTask();
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

    private static Task<Result<bool>> QueueReplication(Replicator replicator, IAuditTrail<IRaftLogEntry> auditTrail, long currentIndex, CancellationToken token)
    {
        var workItem = new ReplicationWorkItem(replicator, auditTrail, currentIndex, token);
        ThreadPool.UnsafeQueueUserWorkItem(workItem, preferLocal: false);
        return workItem.Task;
    }
}