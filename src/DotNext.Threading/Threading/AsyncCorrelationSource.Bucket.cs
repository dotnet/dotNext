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
        private object? userData;
        private volatile IConsumer<WaitNode>? owner;
        private TKey? id;

        internal void Initialize(TKey id, IConsumer<WaitNode> owner, object? userData)
        {
            this.id = id;
            this.owner = owner;
            this.userData = userData;
        }

        internal object? UserData => userData;

        internal bool Match(TKey other, IEqualityComparer<TKey> comparer)
            => comparer.Equals(id, other);

        private protected override void ResetCore()
        {
            owner = null;
            consumedCallback = null;
            id = default;
            userData = null;
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

        ref Action<WaitNode>? IPooledManualResetCompletionSource<WaitNode>.OnConsumed => ref consumedCallback;
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

        private void Remove(WaitNode node)
        {
            Debug.Assert(Monitor.IsEntered(this));

            if (ReferenceEquals(first, node))
                first = node.Next;

            if (ReferenceEquals(last, node))
                last = node.Previous;

            node.Detach();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        void IConsumer<WaitNode>.Invoke(WaitNode node) => Remove(node);

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal bool Remove(TKey expected, in Result<TValue> value, IEqualityComparer<TKey> comparer, out object? userData)
        {
            for (WaitNode? current = first, next; current is not null; current = next)
            {
                next = current.Next;
                if (current.Match(expected, comparer))
                {
                    Remove(current);
                    userData = current.UserData;
                    return value.IsSuccessful ? current.TrySetResult(value.OrDefault()!) : current.TrySetException(value.Error);
                }
            }

            userData = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal unsafe void Drain<T>(delegate*<LinkedValueTaskCompletionSource<TValue>, T, void> action, T arg)
        {
            Debug.Assert(action != null);

            for (LinkedValueTaskCompletionSource<TValue>? current = first, next; current is not null; current = next)
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