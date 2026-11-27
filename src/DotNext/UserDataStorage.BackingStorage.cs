using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext;

using Collections.Concurrent;
using Runtime;
using Threading;

public partial struct UserDataStorage
{
    // provides a storage of typed user data slots
    private sealed class BackingStorageEntry : ICloneable
    {
        // must be reference type, otherwise BackingStorage's table resize loses the concurrent writes
        private ConcurrentGrowableArray<IAtomic> values = new(); // of type boxed Atomic<Optional<T>>[]

        public void CopyTo(int typeIndex, Dictionary<string, object> output)
        {
            CopyToCore(values.Array, typeIndex, output);

            static void CopyToCore(ReadOnlySpan<IAtomic> source, int typeIndex, Dictionary<string, object> output)
            {
                output.EnsureCapacity(source.Length);

                for (var i = 0; i < source.Length; i++)
                {
                    if (source[i].Unwrap() is { } value)
                        output[TypeSlot.ToString(typeIndex, i)] = value;
                }
            }
        }

        public BackingStorageEntry Clone()
        {
            var result = new BackingStorageEntry();
            result.values.Array = CloneCore(values.Array);
            return result;

            static IAtomic[] CloneCore(ReadOnlySpan<IAtomic> source)
            {
                if (source.IsEmpty)
                    return [];

                var result = new IAtomic[source.Length];
                for (var i = 0; i < result.Length; i++)
                {
                    result[i] = source[i].Clone();
                }

                return result;
            }
        }

        object ICloneable.Clone() => Clone();

        public Optional<TValue> Get<TValue>(int index)
            => values.TryGet(index, out var entry)
                ? BoxedValue<Atomic<Optional<TValue>>>.UnsafeUnbox(entry).Value
                : Optional.None<TValue>();

        public void Set<TValue>(int index, TValue value)
            => EnsureSlotAllocated<TValue>(index).Value = value;

        public TValue GetOrSet<TValue>(int index, TValue value, out bool isSet)
            => EnsureSlotAllocated<TValue>(index).GetOrSet(value, out isSet);

        public Optional<TValue> Remove<TValue>(int index)
        {
            Optional<TValue> result;
            if (values.TryGet(index, out var entry))
            {
                BoxedValue<Atomic<Optional<TValue>>>.UnsafeUnbox(entry).Clear(out result);
            }
            else
            {
                result = Optional<TValue>.None;
            }

            return result;
        }

        private ref Atomic<Optional<TValue>> EnsureSlotAllocated<TValue>(int index)
            => ref BoxedValue<Atomic<Optional<TValue>>>.UnsafeUnbox(values.Get<OptionalInitializer<TValue>>(index));
        
        [StructLayout(LayoutKind.Auto)]
        private readonly ref struct OptionalInitializer<TValue> : ConcurrentGrowableArray<IAtomic>.IElementInitializer
        {
            static IAtomic ConcurrentGrowableArray<IAtomic>.IElementInitializer.Create()
                => new Atomic<Optional<TValue>>();
        }
    }
    
    // represents specialized dictionary to store all user data associated with the single object
    private sealed class BackingStorage : ICloneable
    {
        // Each element indexed using UserDataSlot<T>.TypeIndex, the element is allocated lazily
        // Each element in the inner array indexed using UserDataSlot<T>.ValueIndex
        private ConcurrentGrowableArray<BackingStorageEntry?> tables;

        // must be public because CWT dynamically accesses it
        public BackingStorage()
            : this(new BackingStorageEntry[TypeSlot.Count])
        {
        }

        private BackingStorage(BackingStorageEntry?[] entries)
            => tables = new() { Array = entries };
        
        public BackingStorage Copy() => new(Clone(tables.Array));

        object ICloneable.Clone() => Copy();

        public IReadOnlyDictionary<string, object> Dump()
        {
            return DumpCore(tables.Array);

            static IReadOnlyDictionary<string, object> DumpCore(ReadOnlySpan<BackingStorageEntry?> entries)
            {
                var result = new Dictionary<string, object>(entries.Length);

                for (var i = 0; i < entries.Length; i++)
                    entries[i]?.CopyTo(i, result);

                return result;
            }
        }

        private static BackingStorageEntry?[] Clone(ReadOnlySpan<BackingStorageEntry?> source)
        {
            var destination = new BackingStorageEntry?[source.Length];

            for (var i = 0; i < source.Length; i++)
            {
                if (source[i] is { } entry)
                    destination[i] = entry.Clone();
            }

            return destination;
        }
        
        public void CopyTo(BackingStorage destination) => destination.tables.Array = Clone(tables.Array);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Optional<TValue> Get<TValue>(UserDataSlot<TValue> slot)
        {
            Debug.Assert(slot.IsAllocated);

            return TryGetEntry(UserDataSlot<TValue>.TypeIndex) is { } entry
                ? entry.Get<TValue>(slot.ValueIndex)
                : Optional<TValue>.None;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetOrSet<TValue>(UserDataSlot<TValue> slot, TValue value, out bool isSet)
        {
            Debug.Assert(slot.IsAllocated);

            return tables
                .GetOrAdd(UserDataSlot<TValue>.TypeIndex)
                .GetOrSet(slot.ValueIndex, value, out isSet);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<TValue>(UserDataSlot<TValue> slot, TValue value)
        {
            Debug.Assert(slot.IsAllocated);

            tables
                .GetOrAdd(UserDataSlot<TValue>.TypeIndex)
                .Set(slot.ValueIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Optional<TValue> Remove<TValue>(UserDataSlot<TValue> slot)
        {
            Debug.Assert(slot.IsAllocated);

            return TryGetEntry(UserDataSlot<TValue>.TypeIndex) is { } entry
                ? entry.Remove<TValue>(slot.ValueIndex)
                : Optional<TValue>.None;
        }

        private BackingStorageEntry? TryGetEntry(int typeIndex)
            => tables.TryGet(typeIndex, out var entry) ? entry : null;
    }

    /*
     * ConditionalWeakTable is synchronized so we use a bucket of tables
     * to reduce the risk of lock contention. The specific table for the object
     * is based on object's identity hash code.
     */
    private static readonly ConditionalWeakTable<object, BackingStorage>?[] Partitions;

    static UserDataStorage()
    {
        const uint minSize = 8U;
        
        uint size;
        if (AppContext.IsJit)
        {
            size = (uint)Environment.ProcessorCount;
            size += size / 2U;
            size = uint.Max(BitOperations.RoundUpToPowerOf2(size), minSize);
        }
        else
        {
            // For AOT, we don't want to call Environment.ProcessorCount that cannot be interpreted at compile time,
            // so the runtime needs to check for type initialization on every access to the class
            size = minSize;
        }

        Partitions = new ConditionalWeakTable<object, BackingStorage>?[size];
    }
    
    private static ref ConditionalWeakTable<object, BackingStorage>? GetPartition(object source)
    {
        Debug.Assert(BitOperations.IsPow2(Partitions.Length));

        var bucketIndex = RuntimeHelpers.GetHashCode(source) & (Partitions.Length - 1);
        Debug.Assert((uint)bucketIndex < (uint)Partitions.Length);

        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Partitions), bucketIndex);
    }

    private static ConditionalWeakTable<object, BackingStorage> GetOrCreatePartition(object source)
    {
        ref var partition = ref GetPartition(source);
        ConditionalWeakTable<object, BackingStorage> newStorage;
        return partition ?? Interlocked.CompareExchange(ref partition, newStorage = [], null) ?? newStorage;
    }
}