using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Debug = System.Diagnostics.Debug;
using MemoryMarshal = System.Runtime.InteropServices.MemoryMarshal;
using Monitor = System.Threading.Monitor;

namespace DotNext.Threading
{
    using Tasks;

    public partial class AsyncCorrelationSource<TKey, TValue>
    {
        private sealed class WaitNode : LinkedValueTaskCompletionSource<TValue, WaitNode>
        {
            private volatile Bucket? owner;
            internal TKey? Id;

            internal WaitNode(Action<WaitNode> backToPool)
                : base(backToPool)
            {
            }

            internal Bucket Owner
            {
                set => owner = value;
            }

            protected override void BeforeCompleted(Result<TValue> result)
                => Interlocked.Exchange(ref owner, null)?.Remove(this);

            private protected override WaitNode CurrentNode => this;

            internal override WaitNode? CleanupAndGotoNext()
            {
                owner = null;
                return base.CleanupAndGotoNext();
            }
        }

        private sealed class WaitNodePool : ValueTaskPool<TValue, WaitNode>
        {
            internal WaitNodePool(int concurrencyLevel)
                : base(concurrencyLevel)
            {
            }

            protected override WaitNode Create(Action<WaitNode> backToPool)
                => new(backToPool);
        }

        private sealed class Bucket
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

            private bool RemoveCore(WaitNode node)
            {
                Debug.Assert(Monitor.IsEntered(this));

                var inList = false;
                if (ReferenceEquals(first, node))
                {
                    first = node.Next;
                    inList = true;
                }

                if (ReferenceEquals(last, node))
                {
                    last = node.Previous;
                    inList = true;
                }

                inList |= node.IsNotRoot;
                node.Detach();
                return inList;
            }

            [MethodImpl(MethodImplOptions.Synchronized)]
            internal bool Remove(WaitNode node) => RemoveCore(node);

            [MethodImpl(MethodImplOptions.Synchronized)]
            internal bool Remove(TKey expected, TValue value, IEqualityComparer<TKey> comparer)
            {
                for (WaitNode? current = first, next; current is not null; current = next)
                {
                    next = current.Next;
                    if (comparer.Equals(expected, current.Id))
                    {
                        // the node will be removed automatically by consumer
                        return current.TrySetResult(value);
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

        private readonly WaitNodePool pool;

        private Bucket GetBucket(TKey eventId)
        {
            var bucketIndex = unchecked((uint)comparer.GetHashCode(eventId)) % buckets.LongLength;
            Debug.Assert(bucketIndex >= 0 && bucketIndex < buckets.LongLength);

            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buckets), (nint)bucketIndex);
        }
    }
}