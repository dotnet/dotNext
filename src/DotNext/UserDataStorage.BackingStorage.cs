using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext;

public partial struct UserDataStorage
{
    // provides a storage of typed user data slots
    [StructLayout(LayoutKind.Auto)]
    private struct BackingStorageEntry()
    {
        private readonly Lock syncRoot = new();
        private Array? array; // of type Optional<T>[]
        
        public void CopyTo(int typeIndex, Dictionary<string, object> output)
        {
            lock (syncRoot)
            {
                if (array is not null)
                {
                    output.EnsureCapacity(array.Length);

                    for (var i = 0; i < array.Length; i++)
                    {
                        if ((array.GetValue(i) as ISupplier<object?>)?.Invoke() is { } value)
                            output[TypeSlot.ToString(typeIndex, i + 1)] = value;
                    }
                }
            }
        }

        private void CopyFrom(Array? source)
        {
            lock (syncRoot)
            {
                array = source?.Clone() as Array;
            }
        }

        public readonly void CopyTo(BackingStorageEntry destination)
        {
            lock (syncRoot)
            {
                destination.CopyFrom(array);
            }
        }
        
        public readonly Optional<TValue> Get<TValue>(int index)
        {
            lock (syncRoot)
            {
                return Unsafe.As<Optional<TValue>[]>(this.array) is { } array && (uint)index < (uint)array.Length
                    ? Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index)
                    : Optional.None<TValue>();
            }
        }
        
        public TValue GetOrAdd<TValue>(int index, TValue value)
        {
            lock (syncRoot)
            {
                ref var valueRef = ref EnsureSlotAllocated<TValue>(index);
                if (valueRef.HasValue)
                {
                    value = valueRef.ValueOrDefault;
                }
                else
                {
                    valueRef = value;
                }
            }

            return value;
        }

        public void Set<TValue>(int index, TValue value)
        {
            lock (syncRoot)
            {
                EnsureSlotAllocated<TValue>(index) = value;
            }
        }

        private ref Optional<TValue> EnsureSlotAllocated<TValue>(int index)
            => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(EnsureCapacity<TValue>(index)), index);
        
        private Optional<TValue>[] EnsureCapacity<TValue>(int index)
        {
            Debug.Assert(syncRoot.IsHeldByCurrentThread);
            Debug.Assert(this.array is null or Optional<TValue>[]);
            
            // resize if needed
            if (Unsafe.As<Optional<TValue>[]>(this.array) is not { } array)
            {
                this.array = array = new Optional<TValue>[index + 1];
            }
            else if ((uint)index >= (uint)array.Length)
            {
                Array.Resize(ref array, index + 1);
                this.array = array;
            }

            return array;
        }

        public readonly Optional<TValue> Remove<TValue>(int index)
        {
            Optional<TValue> result;

            lock (syncRoot)
            {
                Debug.Assert(this.array is null or Optional<TValue>[]);

                if (Unsafe.As<Optional<TValue>[]>(this.array) is not { } array || (uint)index >= (uint)array.Length)
                {
                    result = Optional.None<TValue>();
                }
                else
                {
                    ref var valueRef = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index);
                    result = valueRef;
                    valueRef = Optional.None<TValue>();
                }
            }

            return result;
        }
    }

    // represents specialized dictionary to store all user data associated with the single object
    private sealed class BackingStorage : ICloneable
    {
        private readonly Lock syncRoot;
        
        // Each element indexed using UserDataSlot<T>.TypeIndex
        // Each element in the inner array indexed using UserDataSlot<T>.ValueIndex
        private volatile BackingStorageEntry[] tables;

        // must be public because CWT dynamically accesses it
        public BackingStorage()
            : this(isEmpty: false)
        {
        }

        private BackingStorage(bool isEmpty)
        {
            syncRoot = new();
            if (isEmpty)
            {
                tables = [];
            }
            else
            {
                Span.Initialize(tables = new BackingStorageEntry[TypeSlot.Count]);
            }
        }
        
        public BackingStorage Copy()
        {
            lock (syncRoot)
            {
                var copy = new BackingStorage(isEmpty: true);
                copy.CopyFrom(tables);
                return copy;
            }
        }

        object ICloneable.Clone() => Copy();

        public IReadOnlyDictionary<string, object> Dump()
        {
            var tables = this.tables;
            var result = new Dictionary<string, object>(tables.Length);

            for (var i = 0; i < tables.Length; i++)
                tables[i].CopyTo(i, result);

            return result;
        }

        private void CopyFrom(ReadOnlySpan<BackingStorageEntry> source)
        {
            var destination = new BackingStorageEntry[source.Length];

            for (var i = 0; i < source.Length; i++)
                source[i].CopyTo(destination[i] = new());

            tables = destination;
        }

        // copy must be atomic operation
        public void CopyTo(BackingStorage destination)
        {
            lock (syncRoot)
            {
                destination.CopyFrom(tables);
            }
        }

        private static Optional<TValue> Get<TValue>(ReadOnlySpan<BackingStorageEntry> tables, int typeIndex, int valueIndex)
            => (uint)typeIndex < (uint)tables.Length
                ? tables[typeIndex].Get<TValue>(valueIndex)
                : Optional.None<TValue>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Optional<TValue> Get<TValue>(UserDataSlot<TValue> slot)
        {
            Debug.Assert(slot.IsAllocated);

            return Get<TValue>(tables, UserDataSlot<TValue>.TypeIndex, slot.ValueIndex);
        }

        private BackingStorageEntry[] Resize(int typeIndex)
        {
            lock (syncRoot)
            {
                var tables = this.tables;
                var length = tables.Length;

                if ((uint)typeIndex >= (uint)length)
                {
                    Array.Resize(ref tables, typeIndex + 1);
                    tables.AsSpan(length).Initialize();
                    this.tables = tables;
                }

                return tables;
            }
        }

        private TValue? GetOrSet<TValue, TSupplier>(int typeIndex, int valueIndex, TSupplier valueFactory)
            where TSupplier : struct, ISupplier<TValue>, allows ref struct
        {
            ref var valueHolder = ref EnsureSlotAllocated(typeIndex);
            var result = valueHolder.Get<TValue>(valueIndex);

            return result.HasValue ? result.ValueOrDefault : valueHolder.GetOrAdd(valueIndex, valueFactory.Invoke());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue? GetOrSet<TValue, TSupplier>(UserDataSlot<TValue> slot, TSupplier valueFactory)
            where TSupplier : struct, ISupplier<TValue>, allows ref struct
        {
            Debug.Assert(slot.IsAllocated);

            return GetOrSet<TValue, TSupplier>(UserDataSlot<TValue>.TypeIndex, slot.ValueIndex, valueFactory);
        }

        private void Set<TValue>(int typeIndex, int valueIndex, TValue value)
            => EnsureSlotAllocated(typeIndex).Set(valueIndex, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<TValue>(UserDataSlot<TValue> slot, TValue value)
        {
            Debug.Assert(slot.IsAllocated);

            Set(UserDataSlot<TValue>.TypeIndex, slot.ValueIndex, value);
        }

        private ref BackingStorageEntry EnsureSlotAllocated(int typeIndex)
            => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(EnsureCapacity(typeIndex)), typeIndex);

        private BackingStorageEntry[] EnsureCapacity(int typeIndex)
        {
            var tables = this.tables;
            if ((uint)typeIndex >= (uint)tables.Length)
                tables = Resize(typeIndex);

            return tables;
        }

        private static Optional<TValue> Remove<TValue>(ReadOnlySpan<BackingStorageEntry> tables, int typeIndex, int valueIndex)
            => (uint)typeIndex < (uint)tables.Length
                ? tables[typeIndex].Remove<TValue>(valueIndex)
                : Optional.None<TValue>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Optional<TValue> Remove<TValue>(UserDataSlot<TValue> slot)
        {
            Debug.Assert(slot.IsAllocated);

            return Remove<TValue>(tables, UserDataSlot<TValue>.TypeIndex, slot.ValueIndex);
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
        uint size = (uint)Environment.ProcessorCount;
        size += size / 2U;
        size = Math.Max(BitOperations.RoundUpToPowerOf2(size), 8U);
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
        return Volatile.Read(in partition) ?? Interlocked.CompareExchange(ref partition, newStorage = [], null) ?? newStorage;
    }
}