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
    [StructLayout(LayoutKind.Auto)]
    private struct BackingStorageEntry()
    {
        private ConcurrentGrowableArray<IAtomic> values = new(); // of type boxed Atomic<Optional<T>>[]

        public readonly void CopyTo(int typeIndex, Dictionary<string, object> output)
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
        
        private void CopyFrom(ReadOnlySpan<IAtomic> source)
        {
            values.Array = source.IsEmpty ? [] : Clone(source);

            static IAtomic[] Clone(ReadOnlySpan<IAtomic> source)
            {
                var result = new IAtomic[source.Length];
                for (var i = 0; i < result.Length; i++)
                {
                    result[i] = source[i].Clone();
                }

                return result;
            }
        }

        public readonly void CopyTo(ref BackingStorageEntry destination)
            => destination.CopyFrom(values.Array);

        public readonly Optional<TValue> Get<TValue>(int index)
        {
            ref var itemRef = ref values.TryGet(index);
            return Unsafe.IsNullRef(ref itemRef)
                ? Optional.None<TValue>()
                : BoxedValue<Atomic<Optional<TValue>>>.UnsafeUnbox(itemRef).Value;
        }

        public void Set<TValue>(int index, TValue value)
            => EnsureSlotAllocated<TValue>(index).Value = value;

        public TValue GetOrSet<TValue>(int index, TValue value, out bool isSet)
            => EnsureSlotAllocated<TValue>(index).GetOrSet(value, out isSet);

        public Optional<TValue> Remove<TValue>(int index)
        {
            ref var itemRef = ref values.TryGet(index);
            Optional<TValue> result;
            if (Unsafe.IsNullRef(ref itemRef))
            {
                result = Optional<TValue>.None;
            }
            else
            {
                BoxedValue<Atomic<Optional<TValue>>>.UnsafeUnbox(itemRef).Clear(out result);
            }

            return result;
        }

        private ref Atomic<Optional<TValue>> EnsureSlotAllocated<TValue>(int index)
            => ref BoxedValue<Atomic<Optional<TValue>>>.UnsafeUnbox(values.Get<OptionalInitializer<TValue>>(index));
        
        [StructLayout(LayoutKind.Auto)]
        private readonly ref struct OptionalInitializer<TValue> : ConcurrentGrowableArray<IAtomic>.IElementInitializer
        {
            static void ConcurrentGrowableArray<IAtomic>.IElementInitializer.Initialize(out IAtomic value)
                => value = new Atomic<Optional<TValue>>();
        }
    }
    
    // represents specialized dictionary to store all user data associated with the single object
    private sealed class BackingStorage : ICloneable
    {
        // Each element indexed using UserDataSlot<T>.TypeIndex
        // Each element in the inner array indexed using UserDataSlot<T>.ValueIndex
        private ConcurrentGrowableArray<BackingStorageEntry> tables;

        // must be public because CWT dynamically accesses it
        public BackingStorage()
            : this(isEmpty: false)
        {
        }

        private BackingStorage(bool isEmpty)
        {
            BackingStorageEntry[] entries;
            if (isEmpty)
            {
                entries = [];
            }
            else
            {
                entries = new BackingStorageEntry[TypeSlot.Count];
                Span.Initialize(entries);
            }

            tables = new() { Array = entries };
        }
        
        public BackingStorage Copy()
        {
            var copy = new BackingStorage(isEmpty: true);
            copy.CopyFrom(tables.Array);
            return copy;
        }

        object ICloneable.Clone() => Copy();

        public IReadOnlyDictionary<string, object> Dump()
        {
            return DumpCore(tables.Array);

            static IReadOnlyDictionary<string, object> DumpCore(ReadOnlySpan<BackingStorageEntry> entries)
            {
                var result = new Dictionary<string, object>(entries.Length);

                for (var i = 0; i < entries.Length; i++)
                    entries[i].CopyTo(i, result);

                return result;
            }
        }

        private void CopyFrom(ReadOnlySpan<BackingStorageEntry> source)
        {
            tables.Array = CopyCore(source);

            static BackingStorageEntry[] CopyCore(ReadOnlySpan<BackingStorageEntry> source)
            {
                var destination = new BackingStorageEntry[source.Length];

                for (var i = 0; i < source.Length; i++)
                {
                    ref var entry = ref destination[i];
                    entry = new();
                    source[i].CopyTo(ref entry);
                }

                return destination;
            }
        }
        
        public void CopyTo(BackingStorage destination) => destination.CopyFrom(tables.Array);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Optional<TValue> Get<TValue>(UserDataSlot<TValue> slot)
        {
            Debug.Assert(slot.IsAllocated);

            ref var itemRef = ref tables.TryGet(UserDataSlot<TValue>.TypeIndex);
            return Unsafe.IsNullRef(ref itemRef)
                ? Optional<TValue>.None
                : itemRef.Get<TValue>(slot.ValueIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetOrSet<TValue>(UserDataSlot<TValue> slot, TValue value, out bool isSet)
        {
            Debug.Assert(slot.IsAllocated);

            return tables
                .Get(UserDataSlot<TValue>.TypeIndex)
                .GetOrSet(slot.ValueIndex, value, out isSet);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<TValue>(UserDataSlot<TValue> slot, TValue value)
        {
            Debug.Assert(slot.IsAllocated);

            tables
                .Get(UserDataSlot<TValue>.TypeIndex)
                .Set(slot.ValueIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Optional<TValue> Remove<TValue>(UserDataSlot<TValue> slot)
        {
            Debug.Assert(slot.IsAllocated);

            ref var itemRef = ref tables.TryGet(UserDataSlot<TValue>.TypeIndex);
            return Unsafe.IsNullRef(ref itemRef)
                ? Optional<TValue>.None
                : itemRef.Remove<TValue>(slot.ValueIndex);
        }
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
        if (RuntimeFeature.IsDynamicCodeSupported)
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