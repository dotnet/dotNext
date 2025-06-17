using System.Threading.Tasks.Sources;

namespace DotNext.Threading;

internal sealed class AsyncAutoResetEventSlim(bool initialState = false) : IValueTaskSource<bool>
{
    private const int SignaledState = 0;
    private const int NotSignaledState = 1;
    private const int CallbackAttachedState = 2;

    private volatile int state = initialState ? NotSignaledState : SignaledState;
    private ManualResetValueTaskSourceCore<bool> source = new() { RunContinuationsAsynchronously = true };

    public void Set()
    {
        if (Interlocked.Exchange(ref state, SignaledState) is CallbackAttachedState)
            source.SetResult(true);
    }

    public ValueTask<bool> WaitAsync() => Interlocked.Increment(ref state) is NotSignaledState
        ? ValueTask.FromResult(true)
        : new(this, source.Version);

    bool IValueTaskSource<bool>.GetResult(short token)
    {
        try
        {
            return source.GetResult(token);
        }
        finally
        {
            source.Reset();
        }
    }

    ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token) => source.GetStatus(token);

    void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        => source.OnCompleted(continuation, state, token, flags);
}