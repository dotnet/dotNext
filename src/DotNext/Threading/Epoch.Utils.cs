using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using IO.Hashing;
using Runtime.ExceptionServices;

public partial class Epoch
{
    internal abstract class DeferredActionNode : IThreadPoolWorkItem
    {
        internal DeferredActionNode? Next;

        protected abstract void Invoke();

        internal void InvokeAndCleanup()
        {
            for (DeferredActionNode? current = this, next; current is not null; current = next)
            {
                next = current.Next;
                current.Next = null; // help GC
                current.Invoke();
            }
        }

        internal void InvokeAndCleanup(ref ExceptionAggregator exceptions)
        {
            for (DeferredActionNode? current = this, next; current is not null; current = next)
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
        }

        internal void InvokeAndCleanup(bool throwOnFirstException)
        {
            if (throwOnFirstException)
            {
                InvokeAndCleanup();
            }
            else
            {
                var exceptions = new ExceptionAggregator();
                InvokeAndCleanup(ref exceptions);
                exceptions.ThrowIfNeeded();
            }
        }

        void IThreadPoolWorkItem.Execute()
        {
            var exceptions = new ExceptionAggregator();
            InvokeAndCleanup(ref exceptions);
            exceptions.ThrowIfNeeded();
        }
    }
    
    private sealed class ActionNode(Action callback) : DeferredActionNode
    {
        protected override void Invoke() => callback();
    }

    private sealed class ActionNode<T>(T arg, Action<T> callback) : DeferredActionNode
    {
        protected override void Invoke() => callback(arg);
    }

    private sealed class WorkItem<TWorkItem>(TWorkItem item) : DeferredActionNode
        where TWorkItem : struct, IThreadPoolWorkItem
    {
        protected override void Invoke() => item.Execute();
    }

    private sealed class Cleanup(IDisposable disposable) : DeferredActionNode
    {
        protected override void Invoke() => disposable.Dispose();
    }

    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay($"{{{nameof(DebugView)}}}")]
    internal struct Entry(uint previous, uint next)
    {
        [ThreadStatic]
        private static int? bucketForCurrentThread;
        
        // cached epoch numbers
        internal readonly uint Previous = previous, Next = next;
        
        // size of the array must be a power of 2 to optimize modulo operation
        private readonly DeferredActionNode?[] callbacks = new DeferredActionNode?[RecommendedSize];
        internal ulong Counter;
        
        private static uint RecommendedSize => BitOperations.RoundUpToPowerOf2((uint)Environment.ProcessorCount + 1U);

        private readonly ref DeferredActionNode? this[int index]
        {
            get
            {
                Debug.Assert((uint)index < (uint)callbacks.Length);

                return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(callbacks), index);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly ref DeferredActionNode? BucketForCurrentThread
        {
            get
            {
                if (bucketForCurrentThread is not { } index)
                {
                    bucketForCurrentThread = index = FNV1a32.Hash(Environment.CurrentManagedThreadId) & (int)(RecommendedSize - 1U);
                }

                return ref this[index];
            }
        }

        internal readonly void Defer(DeferredActionNode node)
            => Defer(ref BucketForCurrentThread, node);

        private static void Defer(ref DeferredActionNode? bucket, DeferredActionNode node)
        {
            DeferredActionNode? current, tmp = bucket;
            do
            {
                current = tmp;
                node.Next = current;
            } while (!ReferenceEquals(tmp = Interlocked.CompareExchange(ref bucket, node, current), current));
        }

        internal readonly DeferredActionNode? DetachLocal() => Interlocked.Exchange(ref BucketForCurrentThread, null);

        internal readonly DetachingEnumerable DetachGlobal() => new(callbacks);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        [ExcludeFromCodeCoverage]
        internal readonly string DebugView
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

                var hasDeferredActions = callbacks.Any(Func.IsNotNull<DeferredActionNode?>());
                return $"Epoch={epoch}, Threads={Counter}, DeferredActions={hasDeferredActions}";
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly ref struct DetachingEnumerable(DeferredActionNode?[] callbacks)
    {
        public DetachingEnumerator GetEnumerator() => new(callbacks);

        public int MaxCount => callbacks.Length;
    }

    [StructLayout(LayoutKind.Auto)]
    internal ref struct DetachingEnumerator(DeferredActionNode?[] callbacks)
    {
        private Span<DeferredActionNode?>.Enumerator enumerator = callbacks.AsSpan().GetEnumerator();

        public DeferredActionNode Current
        {
            readonly get;
            private set;
        }

        public bool MoveNext()
        {
            while (enumerator.MoveNext())
            {
                if (Interlocked.Exchange(ref enumerator.Current, null) is not { } current)
                    continue;
                
                Current = current;
                return true;
            }

            return false;
        }
    }

    [InlineArray(3)]
    private struct EpochEntryCollection
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

    internal struct State
    {
        private uint globalEpoch;
        
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private EpochEntryCollection entries;

        public State()
        {
            entries[0] = new(2, 1);
            entries[1] = new(0, 2);
            entries[2] = new(1, 0);
        }

        [UnscopedRef] internal readonly ReadOnlySpan<Entry> Entries => entries;

        [MethodImpl(MethodImplOptions.NoInlining)] // compiler-level barrier to avoid 'globalEpoch' cached reads
        internal readonly void Defer(DeferredActionNode node) => entries[globalEpoch].Defer(node);

        internal uint Enter()
        {
            var currentEpoch = globalEpoch;
            Interlocked.Increment(ref entries[currentEpoch].Counter); // acts as a barrier for 'globalEpoch' reads
            return currentEpoch;
        }

        internal void Exit(uint epoch)
            => Interlocked.Decrement(ref entries[epoch].Counter);

        [ExcludeFromCodeCoverage]
        internal readonly string GetDebugView(uint epoch) => entries[epoch].DebugView;

        [Conditional("DEBUG")]
        internal readonly void AssertCounters(uint epoch)
        {
            ref readonly var entry = ref entries[epoch];
            Debug.Assert(entry.Counter > 0U);

            var prevEpochIndex = entry.Previous;
            var prevEpochThreads = entries[prevEpochIndex].Counter;

            var nextEpochIndex = entry.Next;
            var nextEpochThreads = entries[nextEpochIndex].Counter;

            Debug.Assert(prevEpochThreads is 0U || nextEpochThreads is 0U,
                $"Epoch #{prevEpochIndex}={prevEpochThreads}, Epoch#{nextEpochIndex}={nextEpochThreads}");
        }

        [UnscopedRef]
        internal SafeToReclaimEpoch TryBumpEpoch(uint currentEpoch)
        {
            ref readonly var currentEpochState = ref entries[currentEpoch];
            var nextEpochIndex = currentEpochState.Next;
            ref readonly var previousEpochState = ref entries[currentEpochState.Previous];
            ref readonly var nextEpochState = ref entries[nextEpochIndex];

            return previousEpochState.Counter is 0U
                   && nextEpochState.Counter is 0U
                   && Interlocked.CompareExchange(ref globalEpoch, nextEpochIndex, currentEpoch) == currentEpoch
                ? new(in previousEpochState)
                : default;
        }

        internal readonly void UnsafeReclaim(ref ExceptionAggregator exceptions)
        {
            foreach (ref readonly var state in entries)
            {
                if (state.Counter > 0UL)
                {
                    throw new InvalidOperationException();
                }

                foreach (var bucket in state.DetachGlobal())
                {
                    bucket.InvokeAndCleanup(ref exceptions);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly ref struct SafeToReclaimEpoch
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly ref readonly Entry reference;

        internal SafeToReclaimEpoch(ref readonly Entry reference)
            => this.reference = ref reference;

        internal bool IsEmpty => Unsafe.IsNullRef(in reference);

        internal DeferredActionNode? ReclaimLocal() => reference.DetachLocal();

        internal DetachingEnumerable ReclaimGlobal() => reference.DetachGlobal();
    }
}