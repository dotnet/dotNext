using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;
using Monitor = System.Threading.Monitor;

namespace DotNext.Threading
{
    public partial class AsyncCorrelationSource<TKey, TValue>
    {
        private sealed partial class WaitNode
        {
            private WaitNode? previous, next;

            internal WaitNode? Next => next;

            internal WaitNode? Previous => previous;

            internal bool IsNotRoot => next is not null || previous is not null;

            internal void Append(WaitNode node)
            {
                next = node;
                node.previous = this;
            }

            internal void Detach()
            {
                if (previous is not null)
                    previous.next = next;
                if (next is not null)
                    next.previous = previous;
                next = previous = null;
            }

            internal partial WaitNode? CleanupAndGotoNext();
        }

        private sealed partial class Bucket
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
    }
}