using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext;

public partial struct UserDataStorage
{
    // provides a storage of typed user data slots
    private sealed class BackingStorageEntry
    {
        private Array? array; // of type Optional<T>[]

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void CopyTo(int typeIndex, Dictionary<string, object> output)
        {
            if (array is not null)
            {
                output.EnsureCapacity(array.Length);

                for (var i = 0; i < array.Length; i++)
                {
                    var value = (array.GetValue(i) as ISupplier<object?>)?.Invoke();
                    if (value is not null)
                        output[UserDataSlot.ToString(typeIndex, i + 1)] = value;
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void CopyFrom(Array? source)
            => array = source?.Clone() as Array;

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void CopyTo(BackingStorageEntry destination)
            => destination.CopyFrom(array);

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal Optional<TValue> Get<TValue>(int index)
        {
            Optional<TValue>[]? array = Unsafe.As<Optional<TValue>[]>(this.array);

            return array is not null && (uint)index < (uint)array.Length
                ? Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index)
                : Optional.None<TValue>();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal TValue GetOrAdd<TValue>(int index, TValue value)
        {
            Debug.Assert(this.array is null or Optional<TValue>[]);

            Optional<TValue>[]? array = Unsafe.As<Optional<TValue>[]>(this.array);

            // resize if needed
            if (array is null)
            {
                this.array = array = new Optional<TValue>[index + 1];
            }
            else if ((uint)index >= (uint)array.Length)
            {
                Array.Resize(ref array, index + 1);
                this.array = array;
            }

            ref Optional<TValue> valueRef = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index);
            if (valueRef.HasValue)
            {
                value = valueRef.OrDefault()!;
            }
            else
            {
                valueRef = value;
            }

            return value;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Set<TValue>(int index, TValue value)
        {
            Debug.Assert(this.array is null or Optional<TValue>[]);

            Optional<TValue>[]? array = Unsafe.As<Optional<TValue>[]>(this.array);

            // resize if needed
            if (array is null)
            {
                this.array = array = new Optional<TValue>[index + 1];
            }
            else if ((uint)index >= (uint)array.Length)
            {
                Array.Resize(ref array, index + 1);
                this.array = array;
            }

            Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index) = value;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal Optional<TValue> Remove<TValue>(int index)
        {
            Debug.Assert(this.array is null or Optional<TValue>[]);

            Optional<TValue>[]? array = Unsafe.As<Optional<TValue>[]>(this.array);
            Optional<TValue> result;

            if (array is null || (uint)index >= (uint)array.Length)
            {
                result = Optional.None<TValue>();
            }
            else
            {
                ref Optional<TValue> valueRef = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index);
                result = valueRef;
                valueRef = Optional.None<TValue>();
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal bool TryAdd<TValue>(int index, TValue value)
        {
            Debug.Assert(this.array is null or Optional<TValue>[]);

            Optional<TValue>[]? array = Unsafe.As<Optional<TValue>[]>(this.array);

            // resize if needed
            if (array is null)
            {
                this.array = array = new Optional<TValue>[index + 1];
            }
            else if ((uint)index >= (uint)array.Length)
            {
                Array.Resize(ref array, index + 1);
                this.array = array;
            }

            ref Optional<TValue> valueRef = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index);

            if (valueRef.HasValue)
                return false;

            valueRef = value;
            return true;
        }
    }

    // represents specialized dictionary to store all user data associated with the single object
    private sealed class BackingStorage : ICloneable
    {
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
            if (isEmpty)
            {
                tables = Array.Empty<BackingStorageEntry>();
            }
            else
            {
                tables = new BackingStorageEntry[3];
                tables.AsSpan().Initialize();
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal BackingStorage Copy()
        {
            var copy = new BackingStorage(isEmpty: true);
            copy.CopyFrom(tables);
            return copy;
        }

        object ICloneable.Clone() => Copy();

        internal IReadOnlyDictionary<string, object> Dump()
        {
            var tables = this.tables;
            var result = new Dictionary<string, object>(tables.Length);

            for (var i = 0; i < tables.Length; i++)
                tables[i].CopyTo(i, result);

            return result;
        }

        private void CopyFrom(BackingStorageEntry[] source)
        {
            var destination = new BackingStorageEntry[source.Length];

            for (var i = 0; i < source.Length; i++)
                source[i].CopyTo(destination[i] = new());

            tables = destination;
        }

        // copy must be atomic operation
        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void CopyTo(BackingStorage destination)
            => destination.CopyFrom(tables);

        private Optional<TValue> Get<TValue>(int typeIndex, int valueIndex)
        {
            var tables = this.tables;

            return (uint)typeIndex < (uint)tables.Length
                ? tables[typeIndex].Get<TValue>(valueIndex)
                : Optional.None<TValue>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Optional<TValue> Get<TValue>(UserDataSlot<TValue> slot)
        {
            Debug.Assert(slot.IsAllocated);

            return Get<TValue>(UserDataSlot<TValue>.TypeIndex, slot.ValueIndex);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private BackingStorageEntry[] Resize(int typeIndex)
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

        private TValue? GetOrSet<TValue, TSupplier>(int typeIndex, int valueIndex, TSupplier valueFactory)
           where TSupplier : struct, ISupplier<TValue>
        {
            var tables = this.tables;

            if ((uint)typeIndex >= (uint)tables.Length)
                tables = Resize(typeIndex);

            var valueHolder = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(tables), typeIndex);
            var result = valueHolder.Get<TValue>(valueIndex);

            return result.HasValue ? result.OrDefault() : valueHolder.GetOrAdd(valueIndex, valueFactory.Invoke());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal TValue? GetOrSet<TValue, TSupplier>(UserDataSlot<TValue> slot, TSupplier valueFactory)
            where TSupplier : struct, ISupplier<TValue>
        {
            Debug.Assert(slot.IsAllocated);

            return GetOrSet<TValue, TSupplier>(UserDataSlot<TValue>.TypeIndex, slot.ValueIndex, valueFactory);
        }

        private void Set<TValue>(int typeIndex, int valueIndex, TValue value)
        {
            var tables = this.tables;

            if ((uint)typeIndex >= (uint)tables.Length)
                tables = Resize(typeIndex);

            Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(tables), typeIndex).Set(valueIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Set<TValue>(UserDataSlot<TValue> slot, TValue value)
        {
            Debug.Assert(slot.IsAllocated);

            Set<TValue>(UserDataSlot<TValue>.TypeIndex, slot.ValueIndex, value);
        }

        private Optional<TValue> Remove<TValue>(int typeIndex, int valueIndex)
        {
            var tables = this.tables;

            return (uint)typeIndex < (uint)tables.Length
                ? tables[typeIndex].Remove<TValue>(valueIndex)
                : Optional.None<TValue>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Optional<TValue> Remove<TValue>(UserDataSlot<TValue> slot)
        {
            Debug.Assert(slot.IsAllocated);

            return Remove<TValue>(UserDataSlot<TValue>.TypeIndex, slot.ValueIndex);
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

    private static ConditionalWeakTable<object, BackingStorage> GetStorage(object source)
    {
        Debug.Assert(BitOperations.IsPow2(Partitions.Length));

        var bucketIndex = RuntimeHelpers.GetHashCode(source) & (Partitions.Length - 1);
        Debug.Assert(bucketIndex >= 0 && bucketIndex < Partitions.Length);

        ref var partition = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(Partitions), bucketIndex);

        ConditionalWeakTable<object, BackingStorage> newStorage;
        return Volatile.Read(ref partition) ?? Interlocked.CompareExchange(ref partition, newStorage = new(), null) ?? newStorage;
    }
}