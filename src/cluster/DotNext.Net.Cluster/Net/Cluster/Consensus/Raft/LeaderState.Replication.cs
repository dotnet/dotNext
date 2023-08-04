using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Membership;
using Threading;
using Timestamp = Diagnostics.Timestamp;
using IDataTransferObject = IO.IDataTransferObject;

internal partial class LeaderState<TMember>
{
    internal sealed class Replicator : TaskCompletionSource<Result<bool>>, ILogEntryConsumer<IRaftLogEntry, Result<bool>>, IClusterConfiguration
    {
        private readonly IClusterConfiguration configuration;
        private readonly TMember member;
        private readonly MemberContext? context;
        private readonly long commitIndex, precedingTerm, term;
        private readonly bool applyConfig;
        private readonly ILogger logger;

        // state
        private long replicationIndex; // reuse as precedingIndex
        private long fingerprint;
        private ConfiguredTaskAwaitable<Result<HeartbeatResult>>.ConfiguredTaskAwaiter replicationAwaiter;

        // TODO: Replace with required init properties in the next version of C#
        internal Replicator(
            TMember member,
            MemberContext? context,
            IClusterConfiguration activeConfig,
            IClusterConfiguration? proposedConfig,
            long commitIndex,
            long term,
            long precedingIndex,
            long precedingTerm,
            ILogger logger)
        {
            this.member = member;
            this.context = context;
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
                applyConfig = activeConfig.Fingerprint == fingerprint;
                configuration = this;
            }
        }

        internal ValueTask<Result<bool>> ReplicateAsync(IAuditTrail<IRaftLogEntry> auditTrail, long currentIndex, CancellationToken token)
        {
            var startIndex = replicationIndex + 1L;
            Debug.Assert(startIndex == member.NextIndex);

            logger.ReplicationStarted(member.EndPoint, startIndex);
            return currentIndex >= startIndex ?
                auditTrail.ReadAsync(this, startIndex, token) :
                ReadAsync<EmptyLogEntry, EmptyLogEntry[]>(Array.Empty<EmptyLogEntry>(), null, token);
        }

        private void Complete()
        {
            try
            {
                var result = replicationAwaiter.GetResult();
                ref var nextIndex = ref member.NextIndex;
                ref var fingerprint = ref member.ConfigurationFingerprint;

                // analyze result and decrease node index when it is out-of-sync with the current node
                if (result.Value is HeartbeatResult.Rejected)
                {
                    fingerprint = 0L;
                    if (nextIndex > 0L)
                        nextIndex -= 1L;

                    logger.ReplicationFailed(member.EndPoint, nextIndex);
                }
                else
                {
                    logger.ReplicationSuccessful(member.EndPoint, nextIndex);
                    nextIndex = replicationIndex + 1L;
                    fingerprint = this.fingerprint;
                }

                SetResult(result.SetValue(result.Value is HeartbeatResult.ReplicatedWithLeaderTerm));
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
            replicationAwaiter = member.InstallSnapshotAsync(term, snapshot, snapshotIndex, token).ConfigureAwait(false).GetAwaiter();
            fingerprint = member.ConfigurationFingerprint; // keep local version unchanged
        }

        private void ReplicateEntries<TEntry, TList>(TList entries, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
            where TList : notnull, IReadOnlyList<TEntry>
        {
            logger.ReplicaSize(member.EndPoint, entries.Count, replicationIndex, precedingTerm, member.ConfigurationFingerprint, fingerprint, applyConfig);
            replicationAwaiter = member.AppendEntriesAsync<TEntry, TList>(term, entries, replicationIndex, precedingTerm, commitIndex, configuration, applyConfig, token).ConfigureAwait(false).GetAwaiter();
            replicationIndex += entries.Count;
        }

        long IClusterConfiguration.Fingerprint => fingerprint;

        long IClusterConfiguration.Length => 0L;

        bool IDataTransferObject.IsReusable => true;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => IDataTransferObject.Empty.WriteToAsync(writer, token);

        bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
            => IDataTransferObject.Empty.TryGetMemory(out memory);

        internal (TMember, MemberContext?) MemberAndContext => (member, context);
    }

    private sealed class ReplicationWorkItem : TaskCompletionSource<Result<bool>>, IThreadPoolWorkItem
    {
        private readonly long currentIndex;
        private CancellationToken token;
        private IAuditTrail<IRaftLogEntry>? auditTrail;
        private ConfiguredValueTaskAwaitable<Result<bool>>.ConfiguredValueTaskAwaiter awaiter;

        internal ReplicationWorkItem(
            TMember member,
            MemberContext context,
            IClusterConfiguration activeConfig,
            IClusterConfiguration? proposedConfig,
            long commitIndex,
            long term,
            long precedingIndex,
            long precedingTerm,
            ILogger logger,
            IAuditTrail<IRaftLogEntry> auditTrail,
            long currentIndex,
            CancellationToken token)
            : base(new Replicator(member, context, activeConfig, proposedConfig, commitIndex, term, precedingIndex, precedingTerm, logger), TaskCreationOptions.RunContinuationsAsynchronously)
        {
            Debug.Assert(auditTrail is not null);

            this.currentIndex = currentIndex;
            this.auditTrail = auditTrail;
            this.token = token;
        }

        internal static (TMember, MemberContext?) GetMemberAndContext(Task<Result<bool>> task)
        {
            Debug.Assert(task is { IsCompleted: true, AsyncState: Replicator });

            return Unsafe.As<Replicator>(task.AsyncState).MemberAndContext;
        }

        private void OnCompleted() => Complete(ref awaiter);

        void IThreadPoolWorkItem.Execute()
        {
            var replicator = Unsafe.As<Replicator>(Task.AsyncState);
            var auditTrail = this.auditTrail;
            var token = this.token;

            Debug.Assert(replicator is not null);
            Debug.Assert(auditTrail is not null);

            // help GC
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

    private readonly AsyncAutoResetEvent replicationEvent;

    [SuppressMessage("Usage", "CA2213", Justification = "Disposed correctly by Dispose() method")]
    private readonly SingleProducerMultipleConsumersCoordinator replicationQueue;

    private ValueTask<bool> WaitForReplicationAsync(Timestamp startTime, TimeSpan period, CancellationToken token)
    {
        // subtract heartbeat processing duration from heartbeat period for better stability
        var delay = period - startTime.Elapsed;
        return replicationEvent.WaitAsync(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, token);
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
            replicationTask = ValueTask.FromException(new InvalidOperationException(ExceptionMessages.LocalNodeNotLeader, e));
        }

        return replicationTask;
    }

    private static Task<Result<bool>> QueueReplication(
        TMember member,
        MemberContext context,
        IClusterConfiguration activeConfig,
        IClusterConfiguration? proposedConfig,
        long commitIndex,
        long term,
        long precedingIndex,
        long precedingTerm,
        ILogger logger,
        IAuditTrail<IRaftLogEntry> auditTrail,
        long currentIndex,
        CancellationToken token)
    {
        var workItem = new ReplicationWorkItem(
            member,
            context,
            activeConfig,
            proposedConfig,
            commitIndex,
            term,
            precedingIndex,
            precedingTerm,
            logger,
            auditTrail,
            currentIndex,
            token);

        ThreadPool.UnsafeQueueUserWorkItem(workItem, preferLocal: false);
        return workItem.Task;
    }
}