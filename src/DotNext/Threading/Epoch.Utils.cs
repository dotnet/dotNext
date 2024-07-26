using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Runtime.ExceptionServices;

public partial class Epoch
{
    internal abstract class CallbackNode : IThreadPoolWorkItem
    {
        internal CallbackNode? Next;

        protected abstract void Invoke();

        internal void InvokeAndCleanup()
        {
            for (CallbackNode? current = this, next; current is not null; current = next)
            {
                next = current.Next;
                current.Next = null; // help GC
                current.Invoke();
            }
        }

        internal ExceptionAggregator InvokeAndCleanupReliably()
        {
            var exceptions = new ExceptionAggregator();
            for (CallbackNode? current = this, next; current is not null; current = next)
            {
                next = current.Next;
                current.Next = null; // help GC

                try
                {
                    current.Invoke();
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            return exceptions;
        }

        internal void InvokeAndCleanupReliablyAndThrowIfNeeded()
            => InvokeAndCleanupReliably().ThrowIfNeeded();

        void IThreadPoolWorkItem.Execute() => InvokeAndCleanupReliablyAndThrowIfNeeded();
    }

    private sealed class ActionNode(Action callback) : CallbackNode
    {
        protected override void Invoke() => callback();
    }

    private sealed class ActionNode<T>(T arg, Action<T> callback) : CallbackNode
    {
        protected override void Invoke() => callback(arg);
    }

    private sealed class WorkItemNode<TWorkItem>(TWorkItem item) : CallbackNode
        where TWorkItem : struct, IThreadPoolWorkItem
    {
        protected override void Invoke() => item.Execute();
    }

    private sealed class CleanupNode(IDisposable disposable) : CallbackNode
    {
        protected override void Invoke() => disposable.Dispose();
    }

    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay($"{{{nameof(DebugView)}}}")]
    internal struct Entry(uint previous, uint next)
    {
        // cached epoch numbers
        internal readonly uint Previous = previous, Next = next;
        internal ulong Counter;
        private volatile CallbackNode? top;

        internal void Push(CallbackNode node)
        {
            CallbackNode? current, tmp = top;
            do
            {
                current = tmp;
                node.Next = current;
            } while (!ReferenceEquals(tmp = Interlocked.CompareExchange(ref top, node, current), current));
        }

        internal CallbackNode? Detach() => Interlocked.Exchange(ref top, null);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [ExcludeFromCodeCoverage]
        internal string DebugView
        {
            get
            {
                var epoch = Previous switch
                {
                    0U => 1U,
                    1U => 2U,
                    2U => 0U,
                    _ => uint.MaxValue,
                };

                return $"Epoch={epoch}, Threads={Counter}, deferredActions={top is not null}";
            }
        }
    }

    [InlineArray(3)]
    private struct State
    {
        private Entry entry;

        [UnscopedRef]
        internal ref Entry this[uint epoch]
        {
            get
            {
                Debug.Assert(epoch <= 2U);

                return ref Unsafe.Add(ref entry, epoch);
            }
        }
    }
}