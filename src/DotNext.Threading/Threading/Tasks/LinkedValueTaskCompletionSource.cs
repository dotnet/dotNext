using System.Diagnostics;
using System.Runtime.InteropServices;

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

    internal unsafe LinkedValueTaskCompletionSource<T>? SetResult<TArg>(delegate*<TArg, Result<T>> finalizer, TArg arg, out bool signaled)
    {
        var detachedQueue = new LinkedList();
        signaled = false;

        for (LinkedValueTaskCompletionSource<T>? current = this, next; current is not null; current = next)
        {
            next = current.CleanupAndGotoNext();
            if (current.TrySetResult(Sentinel.Instance, completionToken: null, finalizer(arg), out var resumable))
                signaled = true;

            if (resumable)
                detachedQueue.Add(current);
        }

        return detachedQueue.First;
    }

    internal LinkedValueTaskCompletionSource<T>? SetCanceled(CancellationToken token, out bool signaled)
    {
        Debug.Assert(token.IsCancellationRequested);

        unsafe
        {
            return SetResult(&CreateException, token, out signaled);
        }

        static Result<T> CreateException(CancellationToken token)
            => new(new OperationCanceledException(token));
    }

    internal LinkedValueTaskCompletionSource<T>? SetException(Exception e, out bool signaled)
    {
        unsafe
        {
            return SetResult(&Result.FromException<T>, e, out signaled);
        }
    }

    internal LinkedValueTaskCompletionSource<T>? SetResult(T value, out bool signaled)
    {
        unsafe
        {
            return SetResult(&Result.FromValue<T>, value, out signaled);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    internal struct LinkedList
    {
        private LinkedValueTaskCompletionSource<T>? first;
        private LinkedValueTaskCompletionSource<T>? last;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal readonly LinkedValueTaskCompletionSource<T>? First => first;

        internal void Add(LinkedValueTaskCompletionSource<T> node)
        {
            Debug.Assert(node is not null);

            if (last is null)
            {
                Debug.Assert(first is null);

                first = last = node;
            }
            else
            {
                Debug.Assert(first is not null);

                last.Append(node);
                last = node;
            }
        }

        internal LinkedValueTaskCompletionSource<T>? Dequeue()
        {
            if (first is { } result)
            {
                Remove(result);
            }
            else
            {
                result = null;
            }

            return result;
        }

        internal bool Remove(LinkedValueTaskCompletionSource<T> node)
        {
            bool isFirst;

            if (isFirst = ReferenceEquals(first, node))
                first = node.Next;

            if (ReferenceEquals(last, node))
                last = node.Previous;

            node.Detach();
            return isFirst;
        }
    }
}