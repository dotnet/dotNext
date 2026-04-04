using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;
using Microsoft.Extensions.Logging;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Diagnostics;
using IO.Log;
using Membership;
using Runtime.CompilerServices;
using Threading;
using Timestamp = Diagnostics.Timestamp;
using IDataTransferObject = IO.IDataTransferObject;

internal partial class LeaderState<TMember>
{
    internal class Replicator : IValueTaskSource<Result<bool>>, ILogEntryConsumer<IRaftLogEntry, Result<bool>>, IResettable
    {
        private readonly ILogger logger;
        private readonly Action continuation;
        internal readonly TMember Member;
        private readonly IClusterConfigurationStorage? configurationStorage;

        // reusable fields
        private long commitIndex, term, precedingTerm;

        // state
        private long replicationIndex; // reuse as precedingIndex
        private ConfiguredTaskAwaitable<Result<HeartbeatResult>>.ConfiguredTaskAwaiter replicationAwaiter;
        private ManualResetValueTaskSourceCore<Result<bool>> completionSource;

        internal Replicator(TMember member, IClusterConfigurationStorage? configurationStorage, ILogger logger)
        {
            Debug.Assert(member is not null);
            Debug.Assert(logger is not null);

            Member = member;
            this.logger = logger;
            this.configurationStorage = configurationStorage;
            continuation = Complete;
        }

        internal long PrecedingIndex => replicationIndex;

        internal long PrecedingTerm
        {
            set => precedingTerm = value;
        }

        internal IFailureDetector? FailureDetector
        {
            init;
            get;
        }

        internal void Initialize(
            long commitIndex,
            long term,
            long precedingIndex)
        {
            this.commitIndex = commitIndex;
            this.term = term;
            replicationIndex = precedingIndex;
        }

        internal void Initialize(
            long commitIndex,
            long term,
            long precedingIndex,
            long precedingTerm)
        {
            Initialize(commitIndex, term, precedingIndex);
            PrecedingTerm = precedingTerm;
        }

        public void Reset()
        {
            replicationAwaiter = default;
            completionSource.Reset();
            replicationAwaiter = default;
        }

        internal ValueTask<Result<bool>> ReplicateAsync(IAuditTrail<IRaftLogEntry> auditTrail, long currentIndex, CancellationToken token)
        {
            var startIndex = replicationIndex + 1L;
            Debug.Assert(startIndex == Member.State.NextIndex);

            logger.ReplicationStarted(Member.EndPoint, startIndex, currentIndex);
            return auditTrail.ReadAsync(this, startIndex, currentIndex, token);
        }

        private void Complete()
        {
            try
            {
                var result = replicationAwaiter.GetResult();
                var replicatedWithCurrentTerm = false;
                ref var replicationState = ref Member.State;

                // analyze result and decrease node index when it is out-of-sync with the current node
                switch (result.Value)
                {
                    case HeartbeatResult.ReplicatedWithLeaderTerm:
                        replicatedWithCurrentTerm = true;
                        goto case HeartbeatResult.Replicated;
                    case HeartbeatResult.Replicated:
                        logger.ReplicationSuccessful(Member.EndPoint, replicationState.NextIndex);
                        replicationState.NextIndex = replicationIndex + 1L;
                        break;
                    default:
                        replicationState.NextIndex = replicationState.PrecedingIndex;
                        logger.ReplicationFailed(Member.EndPoint, replicationState.NextIndex);
                        break;
                }

                completionSource.SetResult(result.SetValue(replicatedWithCurrentTerm));
            }
            catch (Exception e)
            {
                completionSource.SetException(e);
            }
        }

        ValueTask<Result<bool>> ILogEntryConsumer<IRaftLogEntry, Result<bool>>.ReadAsync<TEntry, TList>(TList entries, long? snapshotIndex, CancellationToken token)
        {
            replicationAwaiter = snapshotIndex.HasValue
                ? ReplicateSnapshot(entries[0], snapshotIndex.GetValueOrDefault(), token).ConfigureAwait(false).GetAwaiter()
                : ReplicateEntries<TEntry, TList>(entries, token);

            if (replicationAwaiter.IsCompleted)
                Complete();
            else
                replicationAwaiter.OnCompleted(continuation);

            return new(this, completionSource.Version);
        }

        private async Task<Result<HeartbeatResult>> ReplicateSnapshot<TSnapshot>(TSnapshot snapshot, long snapshotIndex, CancellationToken token)
            where TSnapshot : IRaftLogEntry
        {
            Debug.Assert(snapshot.IsSnapshot);

            logger.InstallingSnapshot(Member.EndPoint, replicationIndex = snapshotIndex);

            var (config, version) = configurationStorage is not null
                ? await configurationStorage.LoadConfigurationAsync(token).ConfigureAwait(false)
                : (IDataTransferObject.Empty, 0L);

            return await Member.InstallSnapshotAsync(term, snapshot, snapshotIndex, config, version, token).ConfigureAwait(false);
        }

        private ConfiguredTaskAwaitable<Result<HeartbeatResult>>.ConfiguredTaskAwaiter ReplicateEntries<TEntry, TList>(TList entries, CancellationToken token)
            where TEntry : IRaftLogEntry
            where TList : IReadOnlyList<TEntry>
        {
            logger.ReplicaSize(Member.EndPoint, entries.Count, replicationIndex, precedingTerm);
            var result = Member.AppendEntriesAsync<TEntry, TList>(term, entries, replicationIndex, precedingTerm, commitIndex, token).ConfigureAwait(false).GetAwaiter();
            replicationIndex += entries.Count;
            return result;
        }

        void IValueTaskSource<Result<bool>>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => completionSource.OnCompleted(continuation, state, token, flags);

        ValueTaskSourceStatus IValueTaskSource<Result<bool>>.GetStatus(short token)
            => completionSource.GetStatus(token);

        Result<bool> IValueTaskSource<Result<bool>>.GetResult(short token)
            => completionSource.GetResult(token);
    }

    private readonly AsyncAutoResetEvent replicationEvent;

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

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
    private static async Task<Result<bool>> SpawnReplicationAsync(Replicator replicator, IAuditTrail<IRaftLogEntry> auditTrail, long currentIndex, CancellationToken token)
    {
        replicator.PrecedingTerm = await auditTrail.GetTermAsync(replicator.PrecedingIndex, token).ConfigureAwait(false);
        return await replicator.ReplicateAsync(auditTrail, currentIndex, token).ConfigureAwait(false);
    }
}