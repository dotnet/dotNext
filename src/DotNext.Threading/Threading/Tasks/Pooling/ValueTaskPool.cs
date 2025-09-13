using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks.Pooling;

/*
 * Represents a pool without any allocations. Assuming that wait queues are organized using
 * linked nodes where each node is a completion source. If so, the node pointers (next/previous)
 * can be used to keep completion sources in the pool. The implementation is thread-safe.
 */
[StructLayout(LayoutKind.Auto)]
internal struct ValueTaskPool<T>(long maximumRetained)
{
    private volatile LinkedValueTaskCompletionSource<T>? first;
    private long count; // volatile

    public ValueTaskPool()
        : this(long.MaxValue)
    {
    }

    public readonly long MaximumRetained => maximumRetained;

    public void Return(LinkedValueTaskCompletionSource<T> node)
    {
        Debug.Assert(node is { Status: ManualResetCompletionSourceStatus.WaitForActivation });
        Debug.Assert(node is { Next: null, Previous: null });

        // try to increment the counter
        for (long current = Atomic.Read(in count), tmp; current < maximumRetained; current = tmp)
        {
            tmp = Interlocked.CompareExchange(ref count, current + 1L, current);
            if (tmp == current)
            {
                ReturnCore(node);
                break;
            }
        }
    }

    private void ReturnCore(LinkedValueTaskCompletionSource<T> node)
    {
        for (LinkedValueTaskCompletionSource<T>? current = first, tmp;; current = tmp)
        {
            node.Next = current;

            tmp = Interlocked.CompareExchange(ref first, node, current);
            if (ReferenceEquals(tmp, current))
                break;
        }
    }

    public TNode Rent<TNode>()
        where TNode : LinkedValueTaskCompletionSource<T>, new()
    {
        var current = first;
        for (LinkedValueTaskCompletionSource<T>? tmp;; current = Unsafe.As<TNode>(tmp))
        {
            if (current is null)
            {
                current = new TNode();
                break;
            }

            tmp = Interlocked.CompareExchange(ref first, current.Next, current);
            if (!ReferenceEquals(tmp, current))
                continue;

            current.Next = null;
            var actualCount = Interlocked.Decrement(ref count);
            Debug.Assert(actualCount >= 0L);
            break;
        }

        Debug.Assert(current is TNode);
        Debug.Assert(current is { Next: null, Previous: null });
        return Unsafe.As<TNode>(current);
    }
}