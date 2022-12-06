using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks;

internal abstract class LinkedValueTaskCompletionSource<T> : ValueTaskCompletionSource<T>
{
    private LinkedValueTaskCompletionSource<T>? previous, next;

    private protected LinkedValueTaskCompletionSource(bool runContinuationsAsynchronously = true)
        : base(runContinuationsAsynchronously)
    {
    }

    internal LinkedValueTaskCompletionSource<T>? Next => next;

    internal LinkedValueTaskCompletionSource<T>? Previous => previous;

    internal void Append(LinkedValueTaskCompletionSource<T> node)
    {
        Debug.Assert(next is null);

        node.previous = this;
        next = node;
    }

    internal void Prepend(LinkedValueTaskCompletionSource<T> node)
    {
        Debug.Assert(previous is null);

        node.next = this;
        previous = node;
    }

    internal void Detach()
    {
        if (previous is not null)
            previous.next = next;

        if (next is not null)
            next.previous = previous;

        next = previous = null;
    }

    internal LinkedValueTaskCompletionSource<T>? CleanupAndGotoNext()
    {
        var next = this.next;
        this.next = previous = null;
        return next;
    }

    private unsafe long ResumeAll<TArg>(delegate*<LinkedValueTaskCompletionSource<T>, TArg, bool> callback, TArg arg)
    {
        Debug.Assert(callback != null);

        var count = 0L;

        for (LinkedValueTaskCompletionSource<T>? current = this, next; current is not null; current = next)
        {
            next = current.CleanupAndGotoNext();

            if (callback(current, arg))
                count++;
        }

        return count;
    }

    internal long TrySetResultAndSentinelToAll(T result)
    {
        unsafe
        {
            return ResumeAll(&TrySetResult, result);
        }

        static bool TrySetResult(LinkedValueTaskCompletionSource<T> source, T result)
            => source.TrySetResult(Sentinel.Instance, result);
    }

    internal long TrySetCanceledAndSentinelToAll(CancellationToken token)
    {
        unsafe
        {
            return ResumeAll(&TrySetCanceled, token);
        }

        static bool TrySetCanceled(LinkedValueTaskCompletionSource<T> source, CancellationToken token)
            => source.TrySetCanceled(Sentinel.Instance, token);
    }

    internal long TrySetExceptionAndSentinelToAll(Exception e)
    {
        unsafe
        {
            return ResumeAll(&TrySetException, e);
        }

        static bool TrySetException(LinkedValueTaskCompletionSource<T> source, Exception reason)
            => source.TrySetException(Sentinel.Instance, reason);
    }
}