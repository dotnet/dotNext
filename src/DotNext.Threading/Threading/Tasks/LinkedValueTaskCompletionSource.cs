using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Tasks;

internal abstract class LinkedValueTaskCompletionSource<T> : ValueTaskCompletionSource<T>
{
    private protected LinkedValueTaskCompletionSource(bool runContinuationsAsynchronously = true)
        : base(runContinuationsAsynchronously)
    {
    }

    internal LinkedValueTaskCompletionSource<T>? Next { get; set; }

    internal LinkedValueTaskCompletionSource<T>? Previous { get; private set; }

    internal void Append(LinkedValueTaskCompletionSource<T> node)
    {
        Debug.Assert(Next is null);

        node.Previous = this;
        Next = node;
    }

    internal void Detach()
    {
        if (Previous is not null)
            Previous.Next = Next;

        if (Next is not null)
            Next.Previous = Previous;

        Next = Previous = null;
    }

    internal LinkedValueTaskCompletionSource<T>? CleanupAndGotoNext()
    {
        var next = Next;
        Next = Previous = null;
        return next;
    }

    internal void Unwind()
    {
        for (LinkedValueTaskCompletionSource<T>? current = this, next; current is not null; current = next)
        {
            next = current.CleanupAndGotoNext();
            current.NotifyConsumer();
        }
    }

    internal LinkedValueTaskCompletionSource<T>? SetResult(in Result<T> result)
    {
        var detachedQueue = new LinkedList();

        for (LinkedValueTaskCompletionSource<T>? current = this, next; current is not null; current = next)
        {
            next = current.CleanupAndGotoNext();
            if (current.TrySetResult(Sentinel.Instance, completionToken: null, in result, out var resumable) && resumable)
                detachedQueue.Add(current);
        }

        return detachedQueue.First;
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

        // true if the first node in the queue is removed
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