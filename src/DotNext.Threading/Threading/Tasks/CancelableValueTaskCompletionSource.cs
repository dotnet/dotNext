using System.Diagnostics;
using System.Threading.Tasks.Sources;

namespace DotNext.Threading.Tasks;

internal sealed class CancelableValueTaskCompletionSource : IValueTaskSource<bool>, IThreadPoolWorkItem
{
    private object? continuationState;

    // possible transitions:
    // null or non-stub => stub (canceled, completed)
    // null => non-stub
    // stub (completed) => stub (canceled)
    // non-stub or stub (completed) => null
    private volatile Action<object?>? continuation;
    private short version = short.MinValue;

    private void Complete()
    {
        // null or non-stub => stub (completed)
        Action<object?>? current, tmp = continuation;
        do
        {
            current = tmp;
            if (current.IsStub)
                return;
        } while (!ReferenceEquals(tmp = Interlocked.CompareExchange(ref continuation, ValueTaskSourceHelpers.CompletedStub, current), current));

        current?.Invoke(continuationState);
    }

    private bool TryReset()
    {
        // null, non-stub, stub (completed) => stub (canceled)
        Action<object?>? current, tmp = continuation;
        do
        {
            current = tmp;
            if (ReferenceEquals(current, ValueTaskSourceHelpers.CanceledStub))
                return false;
        } while (!ReferenceEquals(tmp = Interlocked.CompareExchange(ref continuation, null, current), current));

        return true;
    }

    void IThreadPoolWorkItem.Execute() => Complete();

    bool IValueTaskSource<bool>.GetResult(short token)
    {
        Debug.Assert(token == version);

        continuationState = null;
        version++;
        return TryReset();
    }

    ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token) => continuation is { IsStub: false }
        ? ValueTaskSourceStatus.Succeeded
        : ValueTaskSourceStatus.Pending;

    void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        continuationState = state;

        // null => non-stub
        if (Interlocked.CompareExchange(ref this.continuation, continuation, null) is not null)
        {
            continuation.Invoke(state);
        }
    }

    internal ValueTask<bool> WaitAsync<TAwaiter>(TAwaiter awaiter)
        where TAwaiter : ISupplier<IThreadPoolWorkItem, bool>
    {
        if (!awaiter.Invoke(this))
            Complete();

        return new(this, version);
    }

    internal void Cancel()
    {
        if (Interlocked.Exchange(ref continuation, ValueTaskSourceHelpers.CanceledStub) is { IsStub: false } c)
        {
            c(continuationState);
            continuationState = null;
        }
    }
}

file static class ValueTaskSourceHelpers
{
    internal static readonly Action<object?> CompletedStub = Stub;
    internal static readonly Action<object?> CanceledStub = Stub;

    private static void Stub(object? state) => Debug.Fail("Should never be called");

    extension(Action<object?>? continuation)
    {
        public bool IsStub => continuation?.Method == CompletedStub.Method;
    }
}