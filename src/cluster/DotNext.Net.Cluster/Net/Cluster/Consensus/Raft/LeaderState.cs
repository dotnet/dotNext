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
    private readonly Dictionary<TMember, ReplicationProcess> runningReplications;
    
    [SuppressMessage("Usage", "CA2213", Justification = "False positive.")]
    private volatile CancellationTokenSource? timerCancellation;

    private Task? heartbeatTask;

    internal LeaderState(IRaftStateMachine<TMember> stateMachine, int replicationLag)
        : base(stateMachine)
    {
        timerCancellation = new();
        Token = timerCancellation.Token;
        runningReplications = new(9, ReferenceEqualityComparer.Instance);
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
        get;
        init;
    }
    
    public required long WriteBarrier { get; init; }

    public override CancellationToken Token { get; } // cached to prevent ObjectDisposedException

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task DoHeartbeats(TimeSpan period)
    {
        IReadOnlyCollection<TMember> membersCopy = [];

        for (var forced = false;;)
        {
            // populates running replications on first iteration
            membersCopy = await HandleMembershipChangesAsync(membersCopy, Members).ConfigureAwait(false);
            
            // do not resume suspended callers that came after the barrier, resume them in the next iteration
            replicationQueue.SwitchValve();
            var startTime = new Timestamp();
            
            // in case of forced (initiated programmatically, not by timeout) replication
            // do not change GC latency. Otherwise, in case of high load GC is not able to collect garbage
            using (forced ? default : GCLatencyMode.SustainedLowLatency.Enable())
            {
                // process responses
                var (quorum, hasConsensus) = await ReplicateAsync(out var barrier).ConfigureAwait(false);
                if (GetCommitIndex(barrier, quorum, hasConsensus) is not { } commitIndex)
                    break;

                Debug.Assert(hasConsensus);
                LeaderState.BroadcastTimeMeter.Record(RenewLease(startTime), in MeasurementTags);
                if (commitIndex > AuditTrail.LastCommittedEntryIndex)
                {
                    // majority of nodes accept entries with at least one entry from the current term
                    var count = await AuditTrail
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

            // resume all suspended callers added to the queue concurrently before SwitchValve()
            replicationQueue.Drain();
            forced = await WaitForReplicationAsync(startTime, period, Token).ConfigureAwait(false);
        }
    }

    // When a new member is added or removed, the simplest way to detect this fact is just to compare
    // two collections by reference. If the collections are not equal, perform heavyweight analysis to spawn
    // or stop replication processes.
    private ValueTask<IReadOnlyCollection<TMember>> HandleMembershipChangesAsync(IReadOnlyCollection<TMember> oldMembership,
        IReadOnlyCollection<TMember> newMembership)
        => ReferenceEquals(oldMembership, newMembership)
            ? ValueTask.FromResult(oldMembership)
            : HandleMembershipChangesCoreAsync(newMembership);

    private async ValueTask<IReadOnlyCollection<TMember>> HandleMembershipChangesCoreAsync(IReadOnlyCollection<TMember> members)
    {
        IReadOnlySet<TMember> membersCopy = new HashSet<TMember>(members, ReferenceEqualityComparer.Instance);
        await RemoveMembers(membersCopy).ConfigureAwait(false);
        AddMembers(membersCopy);
        return members;
    }

    private void AddMembers(IReadOnlySet<TMember> members)
    {
        var state = new IRaftClusterMember.ReplicationState();
        state.Initialize(AuditTrail);
        
        foreach (var member in members)
        {
            if (!runningReplications.ContainsKey(member))
            {
                Debug.Assert(member.IsRemote);
                
                member.State = state;
                runningReplications.Add(member, StartReplication(member));
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
        barrier = RentBarrier();
        var task = barrier.WaitAsync(runningReplications.Count, AuditTrail.LastEntryIndex);
        StartReplication(barrier);
        return task;
    }
    
    private ReplicationProcess<TMember> StartReplication(TMember member)
    {
        var process = new ReplicationProcess<TMember>(member, barriers.Capacity)
        {
            Term = Term,
            Logger = Logger,
            AuditTrail = AuditTrail,
            FailureDetector = FailureDetectorFactory?.Invoke(maxLease, member),
            MeasurementTags = MeasurementTags,
        };

        process.Start(Token);
        return process;
    }

    private void StartReplication(ReplicationBarrier barrier)
    {
        var unresponsiveMember = default(TMember);
        foreach (var (member, process) in runningReplications)
        {
            process.Replicate(barrier);
            
            if (!process.IsAvailable && unresponsiveMember is null)
            {
                unresponsiveMember = member;
            }
        }

        // Process 1 unavailable member at a time.
        // UnavailableMemberDetected call doesn't remove the member immediately, it will happen eventually.
        // The removal will be processed eventually in the main loop of the leader state.
        if (unresponsiveMember is not null)
        {
            unresponsiveMember.State.IsAvailable = false;
            UnavailableMemberDetected(unresponsiveMember, Term, Token);
        }
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
                    indexBuffer[i] = result.ReplicatedIndex;
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
    /// <param name="leaderNode">The leader node.</param>
    /// <param name="period">Time period of Heartbeats.</param>
    internal void StartLeading(TMember leaderNode, TimeSpan period)
    {
        runningReplications.Add(leaderNode, new());
        heartbeatTask = DoHeartbeats(period);
        LeaderState.TransitionRateMeter.Add(1, in MeasurementTags);
    }
    
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