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

    private void Defer(uint epoch, nuint handle)
    {
        Debug.Assert(NativeResourceManager is not null);
        Debug.Assert(handle is not 0U);
        
        entries[epoch].Defer(NativeResourceManager, handle);
    }

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

    private Discardable? Reclaim(uint currentEpoch, bool drainGlobalCache, out nuint unmanaged)
    {
        ref readonly var safeToReclaimEpoch = ref TryBumpEpoch(currentEpoch);
        if (Unsafe.IsNullRef(in safeToReclaimEpoch))
        {
            unmanaged = 0U;
            return null;
        }

        return safeToReclaimEpoch.Detach(NativeResourceManager, drainGlobalCache, out unmanaged);
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
        {
            var resourceManager = NativeResourceManager;
            safeToReclaimEpoch.Detach(resourceManager, drainGlobalCache: true, out var unmanaged)?.Drain(ref exceptions);
            if (unmanaged is not 0U)
            {
                Debug.Assert(resourceManager is not null);

                INativeResourceManager.Clear(resourceManager, unmanaged, ref exceptions);
            }
        }

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
        private readonly Resource[] callbacks = new Resource[RecommendedSize];
        internal uint Counter;

        private readonly ref Resource this[int index]
        {
            get
            {
                Debug.Assert((uint)index < (uint)callbacks.Length);

                return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(callbacks), index);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static int IndexForCurrentThread
        {
            get
            {
                if (bucketForCurrentThread is not { } index)
                {
                    bucketForCurrentThread = index = Environment.CurrentManagedThreadId & (int)(RecommendedSize - 1U);
                }

                return index;
            }
        }

        internal readonly void Defer(Discardable node)
            => node.Attach(ref this[IndexForCurrentThread].Managed);

        internal readonly void Defer(INativeResourceManager resourceManager, nuint handle)
            => INativeResourceManager.Attach(resourceManager, handle, ref this[IndexForCurrentThread].Unmanaged);

        internal readonly Discardable? Detach(INativeResourceManager? resourceManager,
            bool drainGlobalCache,
            out nuint unmanaged)
            => drainGlobalCache
                ? DetachGlobal(resourceManager, out unmanaged)
                : DetachLocal(resourceManager, out unmanaged);

        private readonly Discardable? DetachLocal(INativeResourceManager? resourceManager, out nuint unmanaged)
        {
            ref var entry = ref this[IndexForCurrentThread];
            unmanaged = resourceManager is not null ? entry.DetachUnmanaged() : 0U;
            return entry.DetachManaged();
        }

        private readonly Discardable? DetachGlobal(INativeResourceManager? resourceManager, out nuint unmanaged)
        {
            unmanaged = resourceManager is null ? 0U : DetachGlobal(resourceManager);
            return DetachGlobal();
        }

        private readonly nuint DetachGlobal(INativeResourceManager resourceManager)
        {
            nuint root = 0U, last = 0U;
            for (var i = 0; i < callbacks.Length; i++)
            {
                var detached = this[i].DetachUnmanaged();
                if (detached is 0U)
                    continue;

                if (last is 0U)
                {
                    root = detached;
                }
                else
                {
                    resourceManager.GetNextHandle(last) = detached;
                }

                last = INativeResourceManager.GetLast(resourceManager, detached);
            }

            return root;
        }

        private readonly Discardable? DetachGlobal()
        {
            Discardable? root = null, last = null;
            
            // find the first non-null node
            for (var i = 0; i < callbacks.Length; i++)
            {
                if (this[i].DetachManaged() is not { } detached)
                    continue;

                if (last is null)
                {
                    root = detached;
                }
                else
                {
                    last.Attach(detached);
                }

                last = detached.GetLast();
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

                var hasDeferredActions = Array.Exists(callbacks, static r => !r.IsEmpty);
                return $"Epoch={epoch}, Threads={Counter}, DeferredActions={hasDeferredActions}";
            }
        }

        [StructLayout(LayoutKind.Auto)]
        private struct Resource
        {
            public Discardable? Managed;
            public nuint Unmanaged;

            [ExcludeFromCodeCoverage]
            public readonly bool IsEmpty => Managed is null && Unmanaged is 0U;

            public Discardable? DetachManaged() => Discardable.Detach(ref Managed);

            public nuint DetachUnmanaged()
                => Unmanaged is not 0U
                    ? Interlocked.Exchange(ref Unmanaged, 0U)
                    : 0U;
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
    
    partial interface INativeResourceManager
    {
        internal static void Clear(INativeResourceManager resourceManager, nuint rootHandle)
        {
            for (nuint current = rootHandle, next; current is not 0U; current = next)
            {
                next = resourceManager.GetNextHandle(current);
                resourceManager.Release(current);
            }
        }

        internal static void Clear(INativeResourceManager resourceManager, nuint rootHandle, ref ExceptionAggregator exceptions)
        {
            for (nuint current = rootHandle, next; current is not 0U; current = next)
            {
                next = resourceManager.GetNextHandle(current);
                try
                {
                    resourceManager.Release(current);
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }
        }

        internal static nuint GetLast(INativeResourceManager manager, nuint handle)
        {
            Debug.Assert(handle is not 0U);
            
            var result = handle;
            for (nuint next; (next = manager.GetNextHandle(result)) is not 0U;)
            {
                result = next;
            }

            return result;
        }

        internal static void Attach(INativeResourceManager resourceManager, nuint handle, ref nuint root)
        {
            for (nuint current = root, tmp;; current = tmp)
            {
                resourceManager.GetNextHandle(handle) = current;
                tmp = Interlocked.CompareExchange(ref root, handle, current);
                if (current == tmp)
                    break;
            }
        }
    }
}