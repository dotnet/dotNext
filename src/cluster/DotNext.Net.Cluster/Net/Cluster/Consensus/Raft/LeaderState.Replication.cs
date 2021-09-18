using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Membership;
using Threading;

internal partial class LeaderState
{
    internal sealed class Replicator : TaskCompletionSource<Result<bool>>, ILogEntryConsumer<IRaftLogEntry, Result<bool>>
    {
        private readonly IAuditTrail<IRaftLogEntry> auditTrail;
        private readonly IClusterConfiguration activeConfig;
        private readonly IClusterConfiguration? proposedConfig;
        private readonly IRaftClusterMember member;
        private readonly long commitIndex, precedingIndex, precedingTerm, term;
        private readonly ILogger logger;
        private readonly CancellationToken token;

        // state
        private long currentIndex, fingerprint;
        private bool replicatedWithCurrentTerm;
        private ConfiguredTaskAwaitable<Result<bool>>.ConfiguredTaskAwaiter replicationAwaiter;

        // TODO: Replace with required init properties in the next version of C#
        internal Replicator(
            IAuditTrail<IRaftLogEntry> auditTrail,
            IClusterConfiguration activeConfig,
            IClusterConfiguration? proposedConfig,
            IRaftClusterMember member,
            long commitIndex,
            long currentIndex,
            long term,
            long precedingIndex,
            long precedingTerm,
            ILogger logger,
            CancellationToken token)
        {
            this.auditTrail = auditTrail;
            this.activeConfig = activeConfig;
            this.proposedConfig = proposedConfig;
            this.member = member;
            this.precedingIndex = precedingIndex;
            this.precedingTerm = precedingTerm;
            this.commitIndex = commitIndex;
            this.currentIndex = currentIndex;
            this.term = term;
            this.logger = logger;
            this.token = token;
            fingerprint = (proposedConfig ?? activeConfig).Fingerprint;
        }

        private Task<Result<bool>> StartCoreAsync()
        {
            logger.ReplicationStarted(member.EndPoint, currentIndex);
            return (currentIndex >= member.NextIndex ?
                auditTrail.ReadAsync(this, member.NextIndex, token) :
                ReadAsync<EmptyLogEntry, EmptyLogEntry[]>(Array.Empty<EmptyLogEntry>(), null, token)).AsTask();
        }

        // ensure that the replication process is forked
        internal Task<Result<bool>> ReplicateAsync(bool fork = true)
            => fork ? System.Threading.Tasks.Task.Run(StartCoreAsync) : StartCoreAsync();

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
                    member.ConfigurationFingerprint.VolatileWrite(fingerprint);
                    result = result.SetValue(replicatedWithCurrentTerm);
                }
                else
                {
                    member.ConfigurationFingerprint = 0L;
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

        private (IClusterConfiguration, bool) GetConfiguration()
        {
            bool applyConfig;
            IClusterConfiguration configuration;

            if (member.ConfigurationFingerprint == fingerprint)
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
                logger.InstallingSnapshot(currentIndex = snapshotIndex.GetValueOrDefault());
                fingerprint = 0L;
                replicationAwaiter = member.InstallSnapshotAsync(term, entries[0], currentIndex, token).ConfigureAwait(false).GetAwaiter();
            }
            else
            {
                logger.ReplicaSize(member.EndPoint, entries.Count, precedingIndex, precedingTerm);
                var (config, applyConfig) = GetConfiguration();
                replicationAwaiter = member.AppendEntriesAsync<TEntry, TList>(term, entries, precedingIndex, precedingTerm, commitIndex, config, applyConfig, token).ConfigureAwait(false).GetAwaiter();
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

    private readonly AsyncTrigger replicationEvent;
    private volatile TaskCompletionSource replicationQueue;

    private void DrainReplicationQueue()
        => Interlocked.Exchange(ref replicationQueue, new(TaskCreationOptions.RunContinuationsAsynchronously)).SetResult();

    private ValueTask<bool> WaitForReplicationAsync(TimeSpan period, CancellationToken token)
        => replicationEvent.WaitAsync(period, token);

    internal Task ForceReplicationAsync(TimeSpan timeout, CancellationToken token)
    {
        var result = replicationQueue.Task;

        // resume heartbeat loop to force replication
        replicationEvent.Signal();

        // enqueue a new task representing completion callback
        return result.WaitAsync(timeout, token);
    }
}