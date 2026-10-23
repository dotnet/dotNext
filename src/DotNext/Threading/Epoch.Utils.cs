using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Runtime.ExceptionServices;

partial class Epoch
{
    private void Defer(uint epoch, Discardable node)
        => entries[epoch].Defer(node);

    private uint EnterEpoch()
    {
        while (true)
        {
            var currentEpoch = globalEpoch;
            ref var counter = ref entries[currentEpoch].Counter;
            Interlocked.Increment(ref counter); // acts as a barrier for 'globalEpoch' reads

            // Rollback the counter if we're not in the same epoch after the increment
            if (currentEpoch == globalEpoch)
                return currentEpoch;

            Interlocked.Decrement(ref counter);
        }
    }

    private void ExitEpoch(uint epoch)
        => Interlocked.Decrement(ref entries[epoch].Counter);

    private RecycleBin Reclaim(uint currentEpoch, bool drainGlobalCache)
    {
        RecycleBin bin;
        if (TryBumpEpoch(currentEpoch) is not { IsEmpty: false } safeToReclaimEpoch)
        {
            bin = default;
        }
        else if (drainGlobalCache)
        {
            bin = new(safeToReclaimEpoch.Value.DetachGlobal());
        }
        else
        {
            bin = new(safeToReclaimEpoch.Value.DetachLocal());
        }

        return bin;
    }

    private ReadOnlyLocalReference<Entry> TryBumpEpoch(uint currentEpoch)
    {
        ref readonly var currentEpochState = ref entries[currentEpoch];
        var nextEpochIndex = currentEpochState.Next;
        ref readonly var previousEpochState = ref entries[currentEpochState.Previous];

        return previousEpochState.Counter is 0U
               && Interlocked.CompareExchange(ref globalEpoch, nextEpochIndex, currentEpoch) == currentEpoch
            ? new(in previousEpochState)
            : default;
    }
    
    private bool TryReclaimCurrentEpoch(ref ExceptionAggregator exceptions)
    {
        if (TryBumpEpoch(Volatile.Read(ref globalEpoch)) is not { IsEmpty: false } safeToReclaimEpoch)
            return false;

        foreach (var bucket in safeToReclaimEpoch.Value.DetachGlobal())
        {
            bucket.Drain(ref exceptions);
        }

        return true;
    }
    
    private bool TryDrain(ref int progress, ref ExceptionAggregator exceptions)
    {
        while (progress < EpochCount && TryReclaimCurrentEpoch(ref exceptions))
            progress++;

        return progress is EpochCount;
    }

    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay($"{{{nameof(DebugView)}}}")]
    internal struct Entry(uint previous, uint next)
    {
        [ThreadStatic]
        private static int? bucketForCurrentThread;
        
        private static readonly uint RecommendedSize = BitOperations.RoundUpToPowerOf2((uint)Environment.ProcessorCount + 1U);
        
        // cached epoch numbers
        internal readonly uint Previous = previous, Next = next;
        
        // size of the array must be a power of 2 to optimize modulo operation
        private readonly Discardable?[] callbacks = new Discardable?[RecommendedSize];
        internal uint Counter;

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
                    bucketForCurrentThread = index = Environment.CurrentManagedThreadId & (int)(RecommendedSize - 1U);
                }

                return ref this[index];
            }
        }

        internal readonly void Defer(Discardable node)
            => node.Attach(ref BucketForCurrentThread);

        internal readonly Discardable? DetachLocal() => Discardable.Detach(ref BucketForCurrentThread);

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

                var hasDeferredActions = Array.Exists(callbacks, Predicate<Discardable>.IsNotNull);
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
            readonly get => field ?? throw new InvalidOperationException();
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

    [InlineArray(EpochCount)]
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
}