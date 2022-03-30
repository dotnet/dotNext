using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Debug = System.Diagnostics.Debug;

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
                    result = result with { Value = replicatedWithCurrentTerm };
                }
                else
                {
                    member.ConfigurationFingerprint = 0L;

                    unsafe
                    {
                        logger.ReplicationFailed(member.EndPoint, member.NextIndex.UpdateAndGet(&DecrementIndex));
                    }
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

            static long DecrementIndex(long index) => index > 0L ? index - 1L : index;
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
}