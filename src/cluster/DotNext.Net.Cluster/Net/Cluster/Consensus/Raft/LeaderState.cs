using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using Diagnostics;
using ReplicationUtils;
using Runtime.CompilerServices;
using static Runtime.GCLatencyModeExtensions;

internal sealed partial class LeaderState<TMember> : ConsensusState<TMember>
    where TMember : class, IRaftClusterMember
{
    private readonly long currentTerm;
    private readonly Dictionary<TMember, ReplicationProcess> runningReplications;
    
    [SuppressMessage("Usage", "CA2213", Justification = "False positive.")]
    private volatile CancellationTokenSource? timerCancellation;

    private Task? heartbeatTask;

    internal LeaderState(IRaftStateMachine<TMember> stateMachine, int replicationLag)
        : base(stateMachine)
    {
        timerCancellation = new();
        Token = timerCancellation.Token;
        runningReplications = new(ReferenceEqualityComparer.Instance);
        lease = new();
        replicationEvent = new(initialState: false) { MeasurementTags = stateMachine.MeasurementTags };
        replicationQueue = new() { MeasurementTags = stateMachine.MeasurementTags };
        barriers = new(replicationLag);
    }

    public required TimeSpan MaxLease
    {
        init => maxLease = value;
    }

    public required long Term
    {
        init => currentTerm = value;
    }

    public override CancellationToken Token { get; } // cached to prevent ObjectDisposedException

    private void Cancel()
    {
        if (Interlocked.Exchange(ref timerCancellation, null) is { } cts)
        {
            using (cts)
            {
                cts.Cancel(throwOnFirstException: false);
            }
        }
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task DoHeartbeats(TimeSpan period, IPersistentState auditTrail)
    {
        IReadOnlyCollection<TMember> membersCopy = [];

        for (var forced = false;;)
        {
            // populates running replications on first iteration
            membersCopy = await HandleMembershipChangesAsync(membersCopy, Members, auditTrail).ConfigureAwait(false);
            
            // do not resume suspended callers that came after the barrier, resume them in the next iteration
            replicationQueue.SwitchValve();
            var startTime = new Timestamp();
            
            // in case of forced (initiated programmatically, not by timeout) replication
            // do not change GC latency. Otherwise, in case of high load GC is not able to collect garbage
            var latencyScope = forced
                ? default
                : GCLatencyMode.SustainedLowLatency.Enable();
            try
            {
                // process responses
                var (quorum, hasConsensus) = await ReplicateAsync(out var barrier).ConfigureAwait(false);
                if (GetCommitIndex(barrier, quorum, hasConsensus) is not { } commitIndex)
                    break;

                Debug.Assert(hasConsensus);
                RenewLease(startTime.Elapsed);
                UpdateLeaderStickiness();
                if (commitIndex > auditTrail.LastCommittedEntryIndex)
                {
                    // majority of nodes accept entries with at least one entry from the current term
                    var count = await auditTrail
                        .CommitAsync(commitIndex, Token)
                        .ConfigureAwait(false); // commit all entries starting from the first uncommitted index to the end
                    Logger.CommitSuccessful(commitIndex, count);
                }
                else
                {
                    Logger.CommitFailed(quorum, commitIndex);
                }

                barrier.Reuse();
            }
            finally
            {
                latencyScope.Dispose();
                LeaderState.BroadcastTimeMeter.Record(startTime.ElapsedMilliseconds);
            }
            
            // resume all suspended callers added to the queue concurrently before SwitchValve()
            replicationQueue.Drain();
            forced = await WaitForReplicationAsync(startTime, period, Token).ConfigureAwait(false);
        }
    }

    // When a new member is added or removed, the simplest way to detect this fact is just to compare
    // two collections by reference. If the collections are not equal, perform heavyweight analysis to spawn
    // or stop replication processes.
    private ValueTask<IReadOnlyCollection<TMember>> HandleMembershipChangesAsync(IReadOnlyCollection<TMember> oldMembership,
        IReadOnlyCollection<TMember> newMembership,
        IPersistentState auditTrail)
        => ReferenceEquals(oldMembership, newMembership)
            ? ValueTask.FromResult(oldMembership)
            : HandleMembershipChangesCoreAsync(newMembership, auditTrail);

    private async ValueTask<IReadOnlyCollection<TMember>> HandleMembershipChangesCoreAsync(
        IReadOnlyCollection<TMember> members,
        IPersistentState auditTrail)
    {
        IReadOnlySet<TMember> membersCopy = new HashSet<TMember>(members, ReferenceEqualityComparer.Instance);
        await RemoveMembers(membersCopy).ConfigureAwait(false);
        AddMembers(membersCopy, auditTrail);
        return members;
    }

    private void AddMembers(IReadOnlySet<TMember> members, IPersistentState auditTrail)
    {
        var state = new IRaftClusterMember.ReplicationState();
        state.Initialize(auditTrail);

        foreach (var member in members)
        {
            if (!runningReplications.ContainsKey(member))
            {
                member.State = state;
                var process = member.IsRemote
                    ? new ReplicationProcess<TMember>(member, barriers.Capacity)
                    {
                        Term = currentTerm,
                        Logger = Logger,
                        AuditTrail = auditTrail,
                        FailureDetector = FailureDetectorFactory?.Invoke(maxLease, member)
                    }
                    : new ReplicationProcess { AuditTrail = auditTrail };

                process.Start(Token);
                runningReplications.Add(member, process);
            }
        }
    }

    private async ValueTask RemoveMembers(IReadOnlySet<TMember> members)
    {
        var removedMembers = runningReplications.Keys
            .Where(members.DoesntContain)
            .ToHashSet<TMember>(ReferenceEqualityComparer.Instance);

        foreach (var member in removedMembers)
        {
            if (runningReplications.Remove(member, out var process))
            {
                await process.StopAsync(interrupt: true).ConfigureAwait(false);
                process.Dispose();
            }
        }
        
        removedMembers.Clear(); // help GC
    }

    private ValueTask<ReplicationResult> ReplicateAsync(out ReplicationBarrier barrier)
    {
        barrier = StartReplication();
        return barrier.WaitAsync(runningReplications.Count);
    }

    private ReplicationBarrier StartReplication()
    {
        var unresponsiveMember = default(TMember);
        var barrier = RentBarrier();
        foreach (var (member, process) in runningReplications)
        {
            process.Replicate(barrier);
            
            if (!process.IsAvailable && unresponsiveMember is null && member.State.IsAvailable)
            {
                member.State.IsAvailable = false;
                unresponsiveMember = member;
            }
        }

        // process 1 unavailable member at a time
        if (unresponsiveMember is not null)
            UnavailableMemberDetected(unresponsiveMember, Token);

        return barrier;
    }
    
    private async Task StopReplicationAsync()
    {
        Debug.Assert(Token.IsCancellationRequested);
        
        foreach (var process in runningReplications.Values)
        {
            await process.StopAsync().ConfigureAwait(false);
            process.Dispose();
        }
    }

    private long? GetCommitIndex(ReplicationBarrier barrier, int quorum, bool hasConsensus)
    {
        using var indexBuffer = (uint)quorum < (uint)SpanOwner<byte>.StackallocThreshold
            ? stackalloc long[quorum]
            : new SpanOwner<long>(quorum);
        
        for (var i = 0; i < quorum; i++)
        {
            switch (barrier[i])
            {
                case { IsCanceled: true }:
                    throw new OperationCanceledException(Token);
                case { Term: { } higherTerm }:
                    MoveToFollowerState(randomizeTimeout: false, higherTerm);
                    return null;
                case var result:
                    indexBuffer[i] = result.CommitIndex;
                    break;
            }
        }

        if (hasConsensus)
            return GetCommitIndex(indexBuffer.Span);
        
        MoveToFollowerState(randomizeTimeout: false);
        return null;
    }

    private static long GetCommitIndex(Span<long> indices)
    {
        indices.Sort();
        var median = (indices.Length - 1) / 2;
        return indices[median];
    }

    /// <summary>
    /// Starts cluster synchronization.
    /// </summary>
    /// <param name="period">Time period of Heartbeats.</param>
    /// <param name="transactionLog">Transaction log.</param>
    internal void StartLeading(TimeSpan period, IPersistentState transactionLog)
    {
        heartbeatTask = DoHeartbeats(period, transactionLog);
        LeaderState.TransitionRateMeter.Add(1, in MeasurementTags);
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        try
        {
            Cancel();
            replicationEvent.CancelSuspendedCallers(Token);
            await (heartbeatTask ?? Task.CompletedTask).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            
            // heartbeat task is the only background task that can modify the dictionary concurrently
            await StopReplicationAsync().ConfigureAwait(false);
            runningReplications.Clear(); // help GC
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
            Cancel();
            heartbeatTask = null;

            DestroyLease();

            // cancel replication queue
            replicationQueue.Dispose(new NotLeaderException());
            replicationEvent.Dispose();

            heartbeatTask = null;
        }

        base.Dispose(disposing);
    }
}

file static class LeaderState
{
    internal static readonly Histogram<double> BroadcastTimeMeter = Metrics.Instrumentation.ServerSide.CreateHistogram<double>("broadcast-time", unit: "ms", description: "Heartbeat Broadcasting Time");
    internal static readonly Counter<int> TransitionRateMeter = Metrics.Instrumentation.ServerSide.CreateCounter<int>("transitions-to-leader-count", description: "Number of Transitions of Leader State");
}

file static class ReadOnlySetExtensions
{
    public static bool DoesntContain<T>(this IReadOnlySet<T> set, T item) => !set.Contains(item);
}