using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace DotNext.Net.Cluster.Consensus.Raft.ReplicationUtils;

/// <summary>
/// Represents the replication barrier that collects the replication results
/// from the individual cluster members.
/// </summary>
internal class ReplicationBarrier : IValueTaskSource<ReplicationResult>
{
    private const byte ActiveState = 0;
    private const byte CompletedState = 1;
    private const byte ConsumedState = 2;
    private const byte CompletedAndConsumedState = CompletedState | ConsumedState;

    // This number must be reasonable to hold realistic number of cluster member responses.
    // For Raft, the typical number of nodes is 3, 5 or 7.
    private const int InlineBufferSize = 9;

    private readonly Lock syncRoot = new();
    private InlineArray9<MemberResult> inlineResults;
    private MemberResult[]? extraResults;
    private int count, writePos;
    private ReplicationState counters;
    private byte state;
    private ManualResetValueTaskSourceCore<ReplicationResult> completion = new() { RunContinuationsAsynchronously = true };

    public ValueTask<ReplicationResult> WaitAsync(int memberCount)
    {
        Debug.Assert(memberCount > 0);

        count = memberCount;
        counters = new((memberCount >>> 1) + 1);
        writePos = 0;

        if (memberCount > InlineBufferSize)
        {
            memberCount -= InlineBufferSize;

            if (extraResults is null || extraResults.Length < memberCount)
                extraResults = new MemberResult[memberCount];
        }

        return new(this, completion.Version);
    }

    public bool IsCompleted => Volatile.Read(in state) > 0;

    // null if member is not available
    public void SetResult(MemberResult? result)
    {
        bool hasConsensus;
        int writtenCount;
        lock (syncRoot)
        {
            GetResultSlot(writePos++) = result ?? MemberResult.Touched;

            switch (state)
            {
                case ActiveState:
                    if (!result.Analyze(ref counters))
                    {
                        hasConsensus = false;
                    }
                    else if (counters.IsReplicatedOrCommitted)
                    {
                        hasConsensus = true;
                    }
                    else if (writePos == count || counters.IsUnavailable)
                    {
                        hasConsensus = false;
                    }
                    else
                    {
                        goto default;
                    }

                    state = CompletedState;
                    writtenCount = writePos;
                    break;
                case CompletedAndConsumedState when writePos == count:
                    Reset();
                    goto reused;
                default:
                    return;
            }
        }

        completion.SetResult(new(writtenCount, hasConsensus));
        return;

        reused:
        ReuseCore();
    }

    private void Reset()
    {
        Debug.Assert(syncRoot.IsHeldByCurrentThread);
        
        completion.Reset();
        state = ActiveState;
        ClearResults();
    }

    private void ClearResults()
    {
        Span<MemberResult> results = inlineResults;
        Span<MemberResult> extra;
        if (count <= InlineBufferSize)
        {
            results = results.Slice(0, count);
            extra = default;
        }
        else
        {
            extra = extraResults.AsSpan(0, count - InlineBufferSize);
        }

        results.Clear();
        extra.Clear();
    }

    protected virtual void ReuseCore()
    {
    }

    public void Reuse()
    {
        bool canBeReused;
        lock (syncRoot)
        {
            state = CompletedAndConsumedState;
            canBeReused = writePos == count;
            if (canBeReused)
            {
                Reset();
            }
        }

        if (canBeReused)
            ReuseCore();
    }

    private ref MemberResult GetResultSlot(int index)
    {
        Debug.Assert((uint)index < (uint)count);

        Span<MemberResult> results;
        if (index < InlineBufferSize)
        {
            results = inlineResults;
        }
        else
        {
            index -= InlineBufferSize;
            results = extraResults;
        }

        return ref results[index];
    }

    public ref readonly MemberResult this[int index] => ref GetResultSlot(index);

    ReplicationResult IValueTaskSource<ReplicationResult>.GetResult(short token) => completion.GetResult(token);

    ValueTaskSourceStatus IValueTaskSource<ReplicationResult>.GetStatus(short token)
        => completion.GetStatus(token);

    void IValueTaskSource<ReplicationResult>.OnCompleted(Action<object?> continuation, object? obj, short token,
        ValueTaskSourceOnCompletedFlags flags)
        => completion.OnCompleted(continuation, obj, token, flags);
}

file static class MemberResultExtensions
{
    public static bool Analyze(this in MemberResult? result, ref ReplicationState state)
    {
        if (result.HasValue)
            return Nullable.GetValueRefOrDefaultRef(in result).Apply(ref state);

        state.Available--;
        return true;
    }
}