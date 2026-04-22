using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace DotNext.Net.Cluster.Consensus.Raft.Consensus;

/// <summary>
/// Represents the replication barrier that collects the replication results
/// from the individual cluster members.
/// </summary>
internal class ReplicationBarrier : IValueTaskSource<ReplicationResult>, IReplicationCommand
{
    private const byte ActiveState = 0;
    private const byte CompletedState = 1;
    private const byte ConsumedState = 2;
    private const byte CompletedAndConsumedState = CompletedState | ConsumedState;

    private const int InlineBufferSize = 9;

    private readonly Lock syncRoot = new();
    private InlineArray9<MemberResult> inlineResults;
    private MemberResult[]? extraResults;
    private int count, writePos;
    private ReplicationState counters;
    private byte state;
    private ManualResetValueTaskSourceCore<ReplicationResult> completion = new() { RunContinuationsAsynchronously = false };

    public ValueTask<ReplicationResult> WaitAsync(int memberCount)
    {
        Debug.Assert(memberCount > 0);

        count = memberCount;
        counters = new((memberCount >> 1) + 1);
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
    public void SetResult(in MemberResult? result)
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
                        break;
                    }

                    state = CompletedState;
                    writtenCount = writePos;
                    goto completed;
                case CompletedAndConsumedState when writePos == count:
                    Reset();
                    break;
            }
        }

        return;

        completed:
        completion.SetResult(new(writtenCount, hasConsensus));
    }

    bool IReplicationCommand.SetResult(MemberResult? result)
    {
        SetResult(in result);
        return true;
    }

    protected virtual void Reset()
    {
        completion.Reset();
        state = ActiveState;
        Span<MemberResult> results = inlineResults;
        results.Clear();

        results = extraResults;
        results.Clear();
    }

    public void Reuse()
    {
        lock (syncRoot)
        {
            state = CompletedAndConsumedState;
            if (writePos == count)
            {
                Reset();
            }
        }
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
            return Nullable.GetValueRefOrDefaultRef(in result).Analyze(ref state);

        state.Available--;
        return true;
    }
}