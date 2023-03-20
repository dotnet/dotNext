using System.Diagnostics.CodeAnalysis;
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

    internal void Unwind()
    {
        for (LinkedValueTaskCompletionSource<T>? current = this, next; current is not null; current = next)
        {
            next = current.CleanupAndGotoNext();
            current.Resume();
        }
    }

    internal unsafe LinkedValueTaskCompletionSource<T>? SetResult<TArg>(delegate*<LinkedValueTaskCompletionSource<T>, TArg, bool> finalizer, TArg arg)
    {
        LinkedValueTaskCompletionSource<T>? first = null, last = null;

        for (LinkedValueTaskCompletionSource<T>? current = this, next; current is not null; current = next)
        {
            next = current.CleanupAndGotoNext();
            if (finalizer(current, arg))
            {
                Append(ref first, ref last, current);
            }
        }

        return first;
    }

    internal LinkedValueTaskCompletionSource<T>? SetCanceled(CancellationToken token)
    {
        Debug.Assert(token.IsCancellationRequested);

        unsafe
        {
            return SetResult(&TrySetCanceled, token);
        }

        static bool TrySetCanceled(LinkedValueTaskCompletionSource<T> source, CancellationToken token)
            => source.InternalTrySetResult(Sentinel.Instance, completionToken: null, new(new OperationCanceledException(token)));
    }

    internal LinkedValueTaskCompletionSource<T>? SetException(Exception e)
    {
        unsafe
        {
            return SetResult(&TrySetException, e);
        }

        static bool TrySetException(LinkedValueTaskCompletionSource<T> source, Exception e)
            => source.InternalTrySetResult(Sentinel.Instance, completionToken: null, new(e));
    }

    internal LinkedValueTaskCompletionSource<T>? SetResult(T value)
    {
        unsafe
        {
            return SetResult(&TrySetResult, value);
        }

        static bool TrySetResult(LinkedValueTaskCompletionSource<T> source, T value)
            => source.InternalTrySetResult(Sentinel.Instance, completionToken: null, value);
    }

    internal static void Append([NotNull] ref LinkedValueTaskCompletionSource<T>? first, [NotNull] ref LinkedValueTaskCompletionSource<T>? last, LinkedValueTaskCompletionSource<T> source)
    {
        if (first is null || last is null)
        {
            first = last = source;
        }
        else
        {
            last.Append(source);
            last = source;
        }
    }
}