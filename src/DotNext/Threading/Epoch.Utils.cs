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

    private Discardable? Reclaim(uint currentEpoch, bool drainGlobalCache)
    {
        ref readonly var safeToReclaimEpoch = ref TryBumpEpoch(currentEpoch);
        return Unsafe.IsNullRef(in safeToReclaimEpoch)
            ? null
            : safeToReclaimEpoch.Detach(drainGlobalCache);
    }

    private ref readonly Entry TryBumpEpoch(uint currentEpoch)
    {
        ref readonly var currentEpochState = ref entries[currentEpoch];
        var nextEpochIndex = currentEpochState.Next;
        ref readonly var previousEpochState = ref entries[currentEpochState.Previous];

        return ref previousEpochState.Counter is 0U
                   && Interlocked.CompareExchange(ref globalEpoch, nextEpochIndex, currentEpoch) == currentEpoch
            ? ref previousEpochState
            : ref Unsafe.NullRef<Entry>();
    }

    private bool TryReclaimCurrentEpoch(ref ExceptionAggregator exceptions)
    {
        ref readonly var safeToReclaimEpoch = ref TryBumpEpoch(Volatile.Read(ref globalEpoch));
        var result = !Unsafe.IsNullRef(in safeToReclaimEpoch);
        if (result)
            safeToReclaimEpoch.Detach(drainGlobalCache: true)?.Drain(ref exceptions);

        return result;
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

        internal readonly Discardable? Detach(bool drainGlobalCache)
            => drainGlobalCache ? DetachGlobal() : DetachLocal();

        private readonly Discardable? DetachLocal() => Discardable.Detach(ref BucketForCurrentThread);

        private readonly Discardable? DetachGlobal()
        {
            var root = default(Discardable);
            int index;
            
            // find the first non-null node
            for (index = 0; index < callbacks.Length && root is null; index++)
            {
                root = Discardable.Detach(ref this[index]);
            }
            
            // attach nodes from other buckets to the root
            if (root is not null)
            {
                for (var last = root.GetLast(); index < callbacks.Length; index++)
                {
                    if (Discardable.Detach(ref this[index]) is { } discardable)
                    {
                        last.Attach(discardable);
                        last = discardable.GetLast();
                    }
                }
            }

            return root;
        }

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