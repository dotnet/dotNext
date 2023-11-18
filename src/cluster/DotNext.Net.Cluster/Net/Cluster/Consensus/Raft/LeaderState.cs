using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Diagnostics;
using IO.Log;
using Membership;
using Runtime.CompilerServices;
using Threading.Tasks;
using static Threading.LinkedTokenSourceFactory;
using GCLatencyModeScope = Runtime.GCLatencyModeScope;

internal sealed partial class LeaderState<TMember> : RaftState<TMember>
    where TMember : class, IRaftClusterMember
{
    private readonly long currentTerm;
    private readonly CancellationTokenSource timerCancellation;
    internal readonly CancellationToken LeadershipToken; // cached to avoid ObjectDisposedException
    private readonly Task<Result<bool>> localMemberResponse;

    private Task? heartbeatTask;

    internal LeaderState(IRaftStateMachine<TMember> stateMachine, long term, TimeSpan maxLease)
        : base(stateMachine)
    {
        currentTerm = term;
        localMemberResponse = Task.FromResult(new Result<bool> { Term = term, Value = true });
        timerCancellation = new();
        LeadershipToken = timerCancellation.Token;
        this.maxLease = maxLease;
        lease = ExpiredLease.Instance;
        replicationEvent = new(initialState: false) { MeasurementTags = stateMachine.MeasurementTags };
        replicationQueue = new() { MeasurementTags = stateMachine.MeasurementTags };
        context = new();
        replicatorFactory = localReplicatorFactory = CreateDefaultReplicator;
    }

    internal ILeaderStateMetrics? Metrics
    {
        private get;
        init;
    }

    private (long, long, int) ForkHeartbeats(TaskCompletionPipe<Task<Result<bool>>> responsePipe, IAuditTrail<IRaftLogEntry> auditTrail, IClusterConfigurationStorage configurationStorage, IEnumerator<TMember> members, CancellationToken token)
    {
        Replicator replicator;
        Task<Result<bool>> response;
        long commitIndex = auditTrail.LastCommittedEntryIndex,
            currentIndex = auditTrail.LastEntryIndex;

        var majority = 0;

        // send heartbeat in parallel
        for (IClusterConfiguration? activeConfig = configurationStorage.ActiveConfiguration, proposedConfig = configurationStorage.ProposedConfiguration; members.MoveNext(); responsePipe.Add(response, replicator), majority++)
        {
            var member = members.Current;
            if (member.IsRemote)
            {
                var precedingIndex = member.State.PrecedingIndex;

                // fork replication procedure
                replicator = context.GetOrCreate(member, replicatorFactory);
                replicator.Initialize(activeConfig, proposedConfig, commitIndex, currentTerm, precedingIndex);
                response = SpawnReplicationAsync(replicator, auditTrail, currentIndex, token);
            }
            else
            {
                replicator = context.GetOrCreate(member, localReplicatorFactory);
                response = localMemberResponse;
            }
        }

        responsePipe.Complete();
        majority = (majority >> 1) + 1;

        return (currentIndex, commitIndex, majority);
    }

    private MemberResponse ProcessMemberResponse(Task<Result<bool>> response, Replicator replicator, out Result<bool> result)
    {
        var detector = replicator.FailureDetector;
        try
        {
            result = response.GetAwaiter().GetResult();
            detector?.ReportHeartbeat();
            return currentTerm >= result.Term ? MemberResponse.Successful : MemberResponse.HigherTermDetected;
        }
        catch (MemberUnavailableException)
        {
            // goto method epilogue
        }
        catch (OperationCanceledException)
        {
            Unsafe.SkipInit(out result);
            return MemberResponse.Canceled;
        }
        catch (Exception e)
        {
            Logger.LogError(e, ExceptionMessages.UnexpectedError);
        }
        finally
        {
            response.Dispose();
            replicator.Reset();
        }

        Unsafe.SkipInit(out result);
        CheckMemberHealthStatus(detector, replicator.Member);
        return MemberResponse.Exception;
    }

    [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1013", Justification = "False positive")]
    private void CheckMemberHealthStatus(IFailureDetector? detector, TMember member)
    {
        switch (detector)
        {
            case { IsMonitoring: false }:
                Logger.UnknownHealthStatus(member.EndPoint);
                break;
            case { IsHealthy: false }:
                UnavailableMemberDetected(member, LeadershipToken);
                break;
        }
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task DoHeartbeats(TimeSpan period, IAuditTrail<IRaftLogEntry> auditTrail, IClusterConfigurationStorage configurationStorage, IReadOnlyCollection<TMember> members, CancellationToken token)
    {
        var cancellationSource = token.LinkTo(LeadershipToken);

        // cached enumerator allows to avoid memory allocation on every GetEnumerator call inside of the loop
        var enumerator = members.GetEnumerator();
        try
        {
            for (var responsePipe = new TaskCompletionPipe<Task<Result<bool>>>(); !token.IsCancellationRequested; responsePipe.Reset(), ReuseEnumerator(ref members, ref enumerator))
            {
                var startTime = new Timestamp();

                // do not resume suspended callers that came after the barrier, resume them in the next iteration
                replicationQueue.SwitchValve();

                // we want to minimize GC intrusion during replication process
                // (however, it is still allowed in case of system-wide memory pressure, e.g. due to container limits)
                var scope = GCLatencyModeScope.SustainedLowLatency;
                try
                {
                    // Perf: the code in this block is inlined instead of moved to separated method because
                    // we want to prevent allocation of state machine on every call
                    int quorum = 0, commitQuorum = 0, majority;
                    (long currentIndex, long commitIndex, majority) = ForkHeartbeats(responsePipe, auditTrail, configurationStorage, enumerator, token);

                    while (await responsePipe.WaitToReadAsync(token).ConfigureAwait(false))
                    {
                        while (responsePipe.TryRead(out var response, out var replicator))
                        {
                            Debug.Assert(replicator is Replicator);

                            switch (ProcessMemberResponse(response, Unsafe.As<Replicator>(replicator), out var result))
                            {
                                case MemberResponse.Exception:
                                    continue;
                                case MemberResponse.HigherTermDetected:
                                    MoveToFollowerState(randomizeTimeout: false, result.Term);
                                    goto case MemberResponse.Canceled;
                                case MemberResponse.Canceled:
                                    return;
                            }

                            if (++quorum == majority)
                            {
                                RenewLease(startTime.Elapsed);
                                UpdateLeaderStickiness();
                                await configurationStorage.ApplyAsync(token).ConfigureAwait(false);
                            }

                            if (result.Value && ++commitQuorum == majority)
                            {
                                // majority of nodes accept entries with at least one entry from the current term
                                var count = await auditTrail.CommitAsync(currentIndex, token).ConfigureAwait(false); // commit all entries starting from the first uncommitted index to the end
                                Logger.CommitSuccessful(currentIndex, count);
                            }
                        }
                    }

                    if (commitQuorum < majority)
                    {
                        Logger.CommitFailed(quorum, commitIndex);
                    }

                    if (quorum < majority)
                    {
                        MoveToFollowerState(randomizeTimeout: false);
                        return;
                    }
                }
                finally
                {
                    var broadcastTime = startTime.ElapsedMilliseconds;
                    scope.Dispose();
                    Metrics?.ReportBroadcastTime(TimeSpan.FromMilliseconds(broadcastTime));
                    LeaderState.BroadcastTimeMeter.Record(broadcastTime, MeasurementTags);
                }

                // resume all suspended callers added to the queue concurrently before SwitchValve()
                replicationQueue.Drain();

                // wait for heartbeat timeout or forced replication
                await WaitForReplicationAsync(startTime, period, token).ConfigureAwait(false);
            }
        }
        finally
        {
            cancellationSource?.Dispose();
            enumerator.Dispose();
        }
    }

    private void ReuseEnumerator(ref IReadOnlyCollection<TMember> currentList, ref IEnumerator<TMember> enumerator)
    {
        var freshList = Members;
        if (ReferenceEquals(currentList, freshList))
        {
            enumerator.Reset();
        }
        else
        {
            enumerator.Dispose();
            currentList = freshList;
            enumerator = freshList.GetEnumerator();
        }
    }

    /// <summary>
    /// Starts cluster synchronization.
    /// </summary>
    /// <param name="period">Time period of Heartbeats.</param>
    /// <param name="transactionLog">Transaction log.</param>
    /// <param name="configurationStorage">Cluster configuration storage.</param>
    /// <param name="token">The toke that can be used to cancel the operation.</param>
    internal void StartLeading(TimeSpan period, IAuditTrail<IRaftLogEntry> transactionLog, IClusterConfigurationStorage configurationStorage, CancellationToken token)
    {
        var members = Members;
        context = new(members.Count);
        var state = new IRaftClusterMember.ReplicationState
        {
            NextIndex = transactionLog.LastEntryIndex + 1L,
        };

        foreach (var member in members)
        {
            member.State = state;
        }

        heartbeatTask = DoHeartbeats(period, transactionLog, configurationStorage, members, token);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            timerCancellation.Cancel(false);
            replicationEvent.CancelSuspendedCallers(LeadershipToken);
            await (heartbeatTask ?? Task.CompletedTask).ConfigureAwait(false); // may throw OperationCanceledException
        }
        catch (Exception e)
        {
            Logger.LeaderStateExitedWithError(e);
        }
        finally
        {
            Dispose(disposing: true);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timerCancellation.Dispose();
            heartbeatTask = null;

            DestroyLease();

            // cancel replication queue
            replicationQueue.Dispose(new InvalidOperationException(ExceptionMessages.LocalNodeNotLeader));
            replicationEvent.Dispose();

            context.Dispose();
        }

        base.Dispose(disposing);
    }

    private enum MemberResponse
    {
        Successful = 0,
        HigherTermDetected,
        Exception,
        Canceled,
    }
}

internal static class LeaderState
{
    internal static readonly Histogram<double> BroadcastTimeMeter = Metrics.Instrumentation.ServerSide.CreateHistogram<double>("broadcast-time", unit: "ms", description: "Heartbeat Broadcasting Time");
    internal static readonly Counter<int> TransitionRateMeter = Metrics.Instrumentation.ServerSide.CreateCounter<int>("transitions-to-leader-count", description: "Number of Transitions of Leader State");
}