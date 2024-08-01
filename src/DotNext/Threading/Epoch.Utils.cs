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
    /// <summary>
    /// Represents an object which lifetime is controlled by <see cref="Epoch"/> internal Garbage Collector.
    /// </summary>
    public abstract class Discardable : IThreadPoolWorkItem
    {
        internal Discardable? Next;

        /// <summary>
        /// Automatically called by <see cref="Epoch"/> infrastructure to clean up resources associated with this object.
        /// </summary>
        protected abstract void Discard();

        internal void Drain()
        {
            for (Discardable? current = this, next; current is not null; current = next)
            {
                next = current.Next;
                current.Next = null; // help GC
                current.Discard();
            }
        }

        internal void Drain(ref ExceptionAggregator exceptions)
        {
            for (Discardable? current = this, next; current is not null; current = next)
            {
                next = current.Next;
                current.Next = null; // help GC

                try
                {
                    current.Discard();
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }
        }

        internal void Drain(bool throwOnFirstException)
        {
            if (throwOnFirstException)
            {
                Drain();
            }
            else
            {
                var exceptions = new ExceptionAggregator();
                Drain(ref exceptions);
                exceptions.ThrowIfNeeded();
            }
        }

        void IThreadPoolWorkItem.Execute()
        {
            var exceptions = new ExceptionAggregator();
            Drain(ref exceptions);
            exceptions.ThrowIfNeeded();
        }
    }
    
    private sealed class ActionNode(Action callback) : Discardable
    {
        protected override void Discard() => callback();
    }

    private sealed class ActionNode<T>(T arg, Action<T> callback) : Discardable
    {
        protected override void Discard() => callback(arg);
    }

    private sealed class WorkItem<TWorkItem>(TWorkItem item) : Discardable
        where TWorkItem : struct, IThreadPoolWorkItem
    {
        protected override void Discard() => item.Execute();
    }

    private sealed class Cleanup(IDisposable disposable) : Discardable
    {
        protected override void Discard() => disposable.Dispose();
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
        private readonly Discardable?[] callbacks = new Discardable?[RecommendedSize];
        internal ulong Counter;
        
        private static uint RecommendedSize => BitOperations.RoundUpToPowerOf2((uint)Environment.ProcessorCount + 1U);

        private readonly ref Discardable? this[int index]
        {
            get
            {
                Debug.Assert((uint)index < (uint)callbacks.Length);

                return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(callbacks), index);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly ref Discardable? BucketForCurrentThread
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

        internal readonly void Defer(Discardable node)
            => Defer(ref BucketForCurrentThread, node);

        private static void Defer(ref Discardable? bucket, Discardable node)
        {
            Discardable? current, tmp = bucket;
            do
            {
                current = tmp;
                node.Next = current;
            } while (!ReferenceEquals(tmp = Interlocked.CompareExchange(ref bucket, node, current), current));
        }

        internal readonly Discardable? DetachLocal() => Interlocked.Exchange(ref BucketForCurrentThread, null);

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

                var hasDeferredActions = callbacks.Any(Func.IsNotNull<Discardable?>());
                return $"Epoch={epoch}, Threads={Counter}, DeferredActions={hasDeferredActions}";
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly ref struct DetachingEnumerable(Discardable?[] callbacks)
    {
        public DetachingEnumerator GetEnumerator() => new(callbacks);

        public int MaxCount => callbacks.Length;
    }

    [StructLayout(LayoutKind.Auto)]
    internal ref struct DetachingEnumerator(Discardable?[] callbacks)
    {
        private Span<Discardable?>.Enumerator enumerator = callbacks.AsSpan().GetEnumerator();

        public Discardable Current
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
        internal readonly void Defer(Discardable node) => entries[globalEpoch].Defer(node);

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

        internal readonly void UnsafeDrain(ref ExceptionAggregator exceptions)
        {
            foreach (ref readonly var state in entries)
            {
                if (state.Counter > 0UL)
                {
                    throw new InvalidOperationException();
                }

                foreach (var bucket in state.DetachGlobal())
                {
                    bucket.Drain(ref exceptions);
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

        internal Discardable? ReclaimLocal() => reference.DetachLocal();

        internal DetachingEnumerable ReclaimGlobal() => reference.DetachGlobal();
    }
}