using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;
using MemoryMarshal = System.Runtime.InteropServices.MemoryMarshal;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;

public partial class AsyncCorrelationSource<TKey, TValue>
{
    private sealed class WaitNode : LinkedValueTaskCompletionSource<TValue>, IPooledManualResetCompletionSource<WaitNode>
    {
        private Action<WaitNode>? consumedCallback;
        private volatile IConsumer<WaitNode>? owner;
        internal TKey? Id;

        internal IConsumer<WaitNode>? Owner
        {
            set => owner = value;
        }

        private protected override void ResetCore()
        {
            owner = null;
            consumedCallback = null;
            Id = default;
            base.ResetCore();
        }

        internal void Append(WaitNode node) => base.Append(node);

        internal new WaitNode? Next => Unsafe.As<WaitNode>(base.Next);

        internal new WaitNode? Previous => Unsafe.As<WaitNode>(base.Previous);

        protected override void AfterConsumed()
        {
            Interlocked.Exchange(ref owner, null)?.Invoke(this);
            consumedCallback?.Invoke(this);
        }

        internal override WaitNode? CleanupAndGotoNext()
        {
            owner = null;
            return Unsafe.As<WaitNode>(base.CleanupAndGotoNext());
        }

        Action<WaitNode>? IPooledManualResetCompletionSource<WaitNode>.OnConsumed
        {
            set => consumedCallback = value;
        }
    }

    private sealed class Bucket : IConsumer<WaitNode>
    {
        private WaitNode? first, last;

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Add(WaitNode node)
        {
            if (last is null)
            {
                first = last = node;
            }
            else
            {
                last.Append(node);
                last = node;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Remove(WaitNode node)
        {
            if (ReferenceEquals(first, node))
                first = node.Next;

            if (ReferenceEquals(last, node))
                last = node.Previous;

            node.Detach();
        }

        void IConsumer<WaitNode>.Invoke(WaitNode node) => Remove(node);

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal bool Remove(TKey expected, in Result<TValue> value, IEqualityComparer<TKey> comparer)
        {
            for (WaitNode? current = first, next; current is not null; current = next)
            {
                next = current.Next;
                if (comparer.Equals(expected, current.Id))
                {
                    Remove(current);
                    current.Owner = null;
                    return value.IsSuccessful ? current.TrySetResult(value.OrDefault()!) : current.TrySetException(value.Error);
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal unsafe void Drain<T>(delegate*<WaitNode, T, void> action, T arg)
        {
            Debug.Assert(action != null);

            for (WaitNode? current = first, next; current is not null; current = next)
            {
                next = current.CleanupAndGotoNext();
                action(current, arg);
            }

            first = last = null;
        }
    }

    private readonly ValueTaskPool<WaitNode> pool;

    private Bucket GetBucket(TKey eventId)
    {
        var bucketIndex = unchecked((uint)comparer.GetHashCode(eventId)) % buckets.LongLength;
        Debug.Assert(bucketIndex >= 0 && bucketIndex < buckets.LongLength);

        return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buckets), (nint)bucketIndex);
    }
}