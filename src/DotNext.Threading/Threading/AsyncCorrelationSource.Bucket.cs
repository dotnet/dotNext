using System.Diagnostics;
using System.Runtime.CompilerServices;
using MemoryMarshal = System.Runtime.InteropServices.MemoryMarshal;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading;

using Tasks;

public partial class AsyncCorrelationSource<TKey, TValue>
{
    private sealed class WaitNode : LinkedValueTaskCompletionSource<TValue>
    {
        private readonly WeakReference<IConsumer<WaitNode>> ownerRef;
        private object? userData;
        private TKey? id;
        internal short CompletionToken;

        internal WaitNode(IConsumer<WaitNode> owner)
        {
            CompletionToken = InitialCompletionToken;
            ownerRef = new(owner, trackResurrection: false);
        }

        internal void Initialize(TKey id, object? userData)
        {
            this.id = id;
            this.userData = userData;
        }

        internal object? UserData => userData;

        internal bool Match(TKey other, IEqualityComparer<TKey>? comparer)
            => comparer?.Equals(id, other) ?? EqualityComparer<TKey>.Default.Equals(id, other);

        protected override void CleanUp()
        {
            id = default;
            userData = null;
            base.CleanUp();
        }

        internal new WaitNode? Next => Unsafe.As<WaitNode>(base.Next);

        internal new WaitNode? Previous => Unsafe.As<WaitNode>(base.Previous);

        protected override void AfterConsumed()
        {
            if (ownerRef.TryGetTarget(out var owner))
                owner.Invoke(this);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal bool NeedsRemoval => CompletionData is null;
    }

    private sealed class Bucket : IConsumer<WaitNode>
    {
        private WaitNode? first, last, pooled;

        private void Add(WaitNode node)
        {
            Debug.Assert(Monitor.IsEntered(this));

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
        void IConsumer<WaitNode>.Invoke(WaitNode node)
        {
            if (node.NeedsRemoval)
                Remove(node);

            if (pooled is null && node.TryReset(out var freshToken))
            {
                node.CompletionToken = freshToken;
                pooled = node;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal WaitNode? Remove(TKey expected, IEqualityComparer<TKey>? comparer, out short completionToken)
        {
            for (WaitNode? current = first, next; current is not null; current = next)
            {
                next = current.Next;
                if (current.Match(expected, comparer))
                {
                    Remove(current);
                    completionToken = current.CompletionToken; // it cannot be modified concurrently
                    return current;
                }
            }

            completionToken = default;
            return null;
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

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal WaitNode CreateNode(TKey eventId, object? userData)
        {
            WaitNode node;
            if (pooled is null)
            {
                node = new(this);
            }
            else
            {
                node = pooled;
                pooled = null;
            }

            node.Initialize(eventId, userData);

            // we need to add the node to the list before the task construction
            // to ensure that completed node will not be added to the list due to cancellation
            Add(node);
            return node;
        }
    }

    private ref Bucket? GetBucket(TKey eventId)
    {
        var bucketIndex = unchecked((uint)(comparer?.GetHashCode(eventId) ?? EqualityComparer<TKey>.Default.GetHashCode(eventId))) % buckets.LongLength;
        Debug.Assert((uint)bucketIndex < (uint)buckets.LongLength);

        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buckets), (nint)bucketIndex);
    }
}