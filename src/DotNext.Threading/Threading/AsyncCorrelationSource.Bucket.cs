using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading
{
    using Tasks;
    using CallerMustBeSynchronizedAttribute = Runtime.CompilerServices.CallerMustBeSynchronizedAttribute;

    public partial class AsyncCorrelationSource<TKey, TValue>
    {
        private sealed class WaitNode : TaskCompletionSource<TValue>
        {
            private readonly TKey expected;
            private readonly IEqualityComparer<TKey> comparer;
            private WaitNode? previous, next;

            internal WaitNode(TKey value, IEqualityComparer<TKey> comparer)
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
                expected = value;
                this.comparer = comparer;
            }

            internal bool TrySetResult(TKey actual, TValue value)
                => comparer.Equals(expected, actual) && TrySetResult(value);

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

            internal WaitNode? CleanupAndGotoNext()
            {
                var next = this.next;
                this.next = previous = null;
                return next;
            }
        }

        private sealed class Bucket
        {
            private WaitNode? first, last;

            [MethodImpl(MethodImplOptions.Synchronized)]
            internal WaitNode Add(TKey value, IEqualityComparer<TKey> comparer)
            {
                var result = new WaitNode(value, comparer);
                if (last is null)
                {
                    first = last = result;
                }
                else
                {
                    last.Append(result);
                    last = result;
                }

                return result;
            }

            [CallerMustBeSynchronized]
            private bool RemoveCore(WaitNode node)
            {
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
            internal bool Remove(TKey key, TValue value)
            {
                for (WaitNode? current = first, next; current is not null; current = next)
                {
                    next = current.Next;
                    if (current.TrySetResult(key, value))
                        return RemoveCore(current);
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
            }

            internal async Task<TValue> WaitAsync(WaitNode node, TimeSpan timeout, CancellationToken token)
            {
                using (var source = token.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(token) : new CancellationTokenSource())
                {
                    if (ReferenceEquals(node.Task, await Task.WhenAny(node.Task, Task.Delay(timeout, source.Token)).ConfigureAwait(false)))
                    {
                        source.Cancel(); // ensure that timer is cancelled
                        goto exit;
                    }
                }

                if (Remove(node))
                {
                    token.ThrowIfCancellationRequested();
                    throw new TimeoutException();
                }

                exit:
                return await node.Task.ConfigureAwait(false);
            }

            internal async Task<TValue> WaitAsync(WaitNode node, CancellationToken token)
            {
                Debug.Assert(token.CanBeCanceled);
                using (var source = new CancelableCompletionSource<TValue>(TaskCreationOptions.RunContinuationsAsynchronously, token))
                {
                    if (ReferenceEquals(node.Task, await Task.WhenAny(node.Task, source.Task).ConfigureAwait(false)))
                        goto exit;
                }

                if (Remove(node))
                    token.ThrowIfCancellationRequested();

                exit:
                return await node.Task.ConfigureAwait(false);
            }
        }
    }
}