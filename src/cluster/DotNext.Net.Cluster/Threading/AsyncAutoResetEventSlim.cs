using System.Threading.Tasks.Sources;

namespace DotNext.Threading;

internal sealed class AsyncAutoResetEventSlim(bool initialState = false) : IValueTaskSource<bool>
{
    private const int PhaseMask = 0b11;
    private const int SignaledState = 0;
    private const int NotSignaledState = 1;
    private const int CallbackAttachedState = 2;

    // The result passed to Set is stored outside the phase bits, so Increment in WaitAsync never touches it
    private const int ResultFlag = 0b100;

    private volatile int state = initialState ? SignaledState | ResultFlag : NotSignaledState;
    private ManualResetValueTaskSourceCore<bool> source = new() { RunContinuationsAsynchronously = true };

    // If Set is called multiple times without waiting, the last result wins
    public void Set(bool result = true)
    {
        var newState = result ? SignaledState | ResultFlag : SignaledState;
        if ((Interlocked.Exchange(ref state, newState) & PhaseMask) is CallbackAttachedState)
            source.SetResult(result);
    }

    public ValueTask<bool> WaitAsync()
    {
        var current = Interlocked.Increment(ref state);
        return (current & PhaseMask) is NotSignaledState
            ? ValueTask.FromResult((current & ResultFlag) is not 0)
            : new(this, source.Version);
    }

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