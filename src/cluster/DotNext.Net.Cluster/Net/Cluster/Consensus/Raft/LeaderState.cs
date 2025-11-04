using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using DotNext.Net.Cluster.Replication;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Diagnostics;
using IO.Log;
using Membership;
using Runtime.CompilerServices;
using Threading.Tasks;
using GCLatencyModeScope = Runtime.GCLatencyModeScope;

internal sealed partial class LeaderState<TMember> : ConsensusState<TMember>
    where TMember : class, IRaftClusterMember
{
    private readonly long currentTerm;
    private readonly CancellationTokenSource timerCancellation;
    private readonly Task<Result<bool>> localMemberResponse;

    private Task? heartbeatTask;

    internal LeaderState(IRaftStateMachine<TMember> stateMachine, long term, TimeSpan maxLease)
        : base(stateMachine)
    {
        currentTerm = term;
        localMemberResponse = Task.FromResult(new Result<bool> { Term = term, Value = true });
        timerCancellation = new();
        Token = timerCancellation.Token;
        this.maxLease = maxLease;
        lease = new();
        replicationEvent = new(initialState: false) { MeasurementTags = stateMachine.MeasurementTags };
        replicationQueue = new() { MeasurementTags = stateMachine.MeasurementTags };
        context = new();
        replicatorFactory = localReplicatorFactory = CreateDefaultReplicator;
    }

    public override CancellationToken Token { get; } // cached to prevent ObjectDisposedException

    private (long, long, int) ForkHeartbeats(TaskCompletionPipe<Task<Result<bool>>> responsePipe, IAuditTrail<IRaftLogEntry> auditTrail, IClusterConfigurationStorage configurationStorage, IEnumerator<TMember> members)
    {
        Replicator replicator;
        Task<Result<bool>> response;
        long commitIndex = auditTrail.LastCommittedEntryIndex,
            currentIndex = auditTrail.LastEntryIndex;

        var majority = 0;

        // send heartbeat in parallel
        for (IClusterConfiguration? activeConfig = configurationStorage.ActiveConfiguration, proposedConfig = configurationStorage.ProposedConfiguration; members.MoveNext() && !Token.IsCancellationRequested; responsePipe.Add(response, replicator), majority++)
        {
            var member = members.Current;
            if (member.IsRemote)
            {
                var precedingIndex = member.State.PrecedingIndex;

                // fork replication procedure
                replicator = context.GetOrCreate(member, replicatorFactory, Token);
                replicator.Initialize(activeConfig, proposedConfig, commitIndex, currentTerm, precedingIndex);
                response = SpawnReplicationAsync(replicator, auditTrail, currentIndex, Token);
            }
            else
            {
                replicator = context.GetOrCreate(member, localReplicatorFactory, Token);
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
            return currentTerm >= result.Term
                ? MemberResponse.Successful
                : MemberResponse.HigherTermDetected;
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

    private void CheckMemberHealthStatus(IFailureDetector? detector, TMember member)
    {
        switch (detector)
        {
            case { IsMonitoring: false }:
                Logger.UnknownHealthStatus(member.EndPoint);
                break;
            case { IsHealthy: false }:
                UnavailableMemberDetected(member, Token);
                break;
        }
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task DoHeartbeats(TimeSpan period, IAuditTrail<IRaftLogEntry> auditTrail, IClusterConfigurationStorage configurationStorage, IReadOnlyCollection<TMember> members)
    {
        // cached enumerator allows to avoid memory allocation on every GetEnumerator call inside of the loop
        var enumerator = members.GetEnumerator();
        try
        {
            var forced = false;
            for (var responsePipe = new TaskCompletionPipe<Task<Result<bool>>>(); !Token.IsCancellationRequested; responsePipe.Reset(), ReuseEnumerator(ref members, ref enumerator))
            {
                var startTime = new Timestamp();

                // do not resume suspended callers that came after the barrier, resume them in the next iteration
                replicationQueue.SwitchValve();

                // in case of forced (initiated programmatically, not by timeout) replication
                // do not change GC latency. Otherwise, in case of high load GC is not able to collect garbage
                var latencyScope = forced
                    ? default
                    : GCLatencyModeScope.SustainedLowLatency;
                try
                {
                    // Perf: the code in this block is inlined instead of moved to separated method because
                    // we want to prevent allocation of state machine on every call
                    int quorum = 0, commitQuorum = 0;
                    (long currentIndex, long commitIndex, var majority) = ForkHeartbeats(responsePipe, auditTrail, configurationStorage, enumerator);

                    while (await responsePipe.WaitToReadAsync().ConfigureAwait(false))
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
                                case MemberResponse.Successful when ++quorum == majority:
                                    RenewLease(startTime.Elapsed);
                                    UpdateLeaderStickiness();
                                    goto default;
                                default:
                                    commitQuorum += Unsafe.BitCast<bool, byte>(result.Value);
                                    continue;
                            }
                        }
                    }

                    if (commitQuorum >= majority)
                    {
                        // majority of nodes accept entries with at least one entry from the current term
                        var count = await auditTrail.CommitAsync(currentIndex, Token).ConfigureAwait(false); // commit all entries starting from the first uncommitted index to the end
                        Logger.CommitSuccessful(currentIndex, count);
                    }
                    else
                    {
                        Logger.CommitFailed(quorum, commitIndex);
                    }

                    if (quorum >= majority)
                    {
                        await configurationStorage.ApplyAsync(Token).ConfigureAwait(false);
                    }
                    else
                    {
                        MoveToFollowerState(randomizeTimeout: false);
                        return;
                    }
                }
                finally
                {
                    var broadcastTime = startTime.ElapsedMilliseconds;
                    latencyScope.Dispose();
                    LeaderState.BroadcastTimeMeter.Record(broadcastTime, MeasurementTags);
                }

                // resume all suspended callers added to the queue concurrently before SwitchValve()
                replicationQueue.Drain();

                // wait for heartbeat timeout or forced replication
                forced = await WaitForReplicationAsync(startTime, period, Token).ConfigureAwait(false);
            }
        }
        finally
        {
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
    internal void StartLeading(TimeSpan period, IAuditTrail<IRaftLogEntry> transactionLog, IClusterConfigurationStorage configurationStorage)
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

        heartbeatTask = DoHeartbeats(period, transactionLog, configurationStorage, members);
        LeaderState.TransitionRateMeter.Add(1, in MeasurementTags);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            timerCancellation.Cancel(throwOnFirstException: false);
            replicationEvent.CancelSuspendedCallers(Token);
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
            replicationQueue.Dispose(new NotLeaderException());
            replicationEvent.Dispose();

            context.Dispose();
            heartbeatTask = null;
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

file static class LeaderState
{
    internal static readonly Histogram<double> BroadcastTimeMeter = Metrics.Instrumentation.ServerSide.CreateHistogram<double>("broadcast-time", unit: "ms", description: "Heartbeat Broadcasting Time");
    internal static readonly Counter<int> TransitionRateMeter = Metrics.Instrumentation.ServerSide.CreateCounter<int>("transitions-to-leader-count", description: "Number of Transitions of Leader State");
}