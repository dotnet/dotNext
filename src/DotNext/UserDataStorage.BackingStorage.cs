using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext;

using Runtime;
using Threading;

public partial struct UserDataStorage
{
    // provides a storage of typed user data slots
    [StructLayout(LayoutKind.Auto)]
    private struct BackingStorageEntry()
    {
        private readonly System.Threading.Lock syncRoot = new();
        private ICloneableBox[] array = []; // of type boxed Atomic<Optional<T>>[]

        public readonly void CopyTo(int typeIndex, Dictionary<string, object> output)
        {
            lock (syncRoot)
            {
                output.EnsureCapacity(array.Length);

                for (var i = 0; i < array.Length; i++)
                {
                    if ((array[i].Value as ISupplier<object?>)?.Invoke() is { } value)
                        output[TypeSlot.ToString(typeIndex, i)] = value;
                }
            }
        }
        
        private void CopyFrom(ICloneableBox[] source)
        {
            lock (syncRoot)
            {
                array = source.Length > 0 ? Clone(source) : [];
            }

            static ICloneableBox[] Clone(ReadOnlySpan<ICloneableBox> source)
            {
                var result = new ICloneableBox[source.Length];
                for (var i = 0; i < result.Length; i++)
                {
                    result[i] = Unsafe.As<ICloneableBox>(source[i].Clone());
                }

                return result;
            }
        }
        
        public readonly void CopyTo(ref BackingStorageEntry destination)
        {
            lock (syncRoot)
            {
                destination.CopyFrom(array);
            }
        }

        public readonly Optional<TValue> Get<TValue>(int index)
        {
            var arrayCopy = Volatile.Read(in array);
            return (uint)index < (uint)arrayCopy.Length
                ? UnsafeGet<TValue>(arrayCopy, index).Value
                : Optional.None<TValue>();
        }

        public void Set<TValue>(int index, TValue value)
            => EnsureSlotAllocated<TValue>(index).Value = value;

        public TValue GetOrSet<TValue>(int index, TValue value, out bool isSet)
            => EnsureSlotAllocated<TValue>(index).GetOrSet(value, out isSet);

        public Optional<TValue> Remove<TValue>(int index)
        {
            Optional<TValue> result;
            if (Volatile.Read(in array) is { } arrayCopy && (uint)index < (uint)arrayCopy.Length)
            {
                UnsafeGet<TValue>(arrayCopy, index).Clear(out result);
            }
            else
            {
                result = Optional<TValue>.None;
            }

            return result;
        }

        private ref Atomic<Optional<TValue>> EnsureSlotAllocated<TValue>(int index)
        {
            var arrayCopy = Volatile.Read(in array);
            if ((uint)index >= (uint)arrayCopy.Length)
                arrayCopy = EnsureCapacity<TValue>(index);

            return ref UnsafeGet<TValue>(arrayCopy, index);
        }

        private static ref Atomic<Optional<TValue>> UnsafeGet<TValue>(ICloneableBox[] array, int index)
        {
            var element = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index);
            return ref BoxedValue<Atomic<Optional<TValue>>>.UnsafeUnbox(element);
        }

        private ICloneableBox[] EnsureCapacity<TValue>(int index)
        {
            ICloneableBox[] arrayCopy;
            lock (syncRoot)
            {
                arrayCopy = array;
                var length = arrayCopy.Length;
                if ((uint)index >= (uint)length)
                {
                    Array.Resize(ref arrayCopy, index + 1);
                    Initialize(arrayCopy.AsSpan(length));
                    array = arrayCopy;
                }
            }

            return arrayCopy;

            static void Initialize(Span<ICloneableBox> slots)
            {
                foreach (ref var slot in slots)
                {
                    slot = new Atomic<Optional<TValue>>();
                }
            }
        }
    }
    
    // represents specialized dictionary to store all user data associated with the single object
    private sealed class BackingStorage : ICloneable
    {
        private readonly System.Threading.Lock syncRoot;
        
        // Each element indexed using UserDataSlot<T>.TypeIndex
        // Each element in the inner array indexed using UserDataSlot<T>.ValueIndex
        private BackingStorageEntry[] tables;

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
            var tablesCopy = Volatile.Read(in tables);
            var result = new Dictionary<string, object>(tablesCopy.Length);

            for (var i = 0; i < tablesCopy.Length; i++)
                tablesCopy[i].CopyTo(i, result);

            return result;
        }

        private void CopyFrom(ReadOnlySpan<BackingStorageEntry> source)
        {
            Debug.Assert(syncRoot.IsHeldByCurrentThread);
            
            var destination = new BackingStorageEntry[source.Length];

            for (var i = 0; i < source.Length; i++)
            {
                ref var entry = ref destination[i];
                entry = new();
                source[i].CopyTo(ref entry);
            }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Optional<TValue> Get<TValue>(UserDataSlot<TValue> slot)
        {
            Debug.Assert(slot.IsAllocated);

            return GetCore(Volatile.Read(in tables), UserDataSlot<TValue>.TypeIndex, slot.ValueIndex);
            
            static Optional<TValue> GetCore(ReadOnlySpan<BackingStorageEntry> tables, int typeIndex, int valueIndex)
                => (uint)typeIndex < (uint)tables.Length
                    ? tables[typeIndex].Get<TValue>(valueIndex)
                    : Optional.None<TValue>();
        }

        private BackingStorageEntry[] Resize(int typeIndex)
        {
            BackingStorageEntry[] tablesCopy;
            lock (syncRoot)
            {
                tablesCopy = tables;
                var length = tablesCopy.Length;

                if ((uint)typeIndex >= (uint)length)
                {
                    Array.Resize(ref tablesCopy, typeIndex + 1);
                    tablesCopy.AsSpan(length).Initialize();
                    tables = tablesCopy;
                }
            }

            return tablesCopy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TValue GetOrSet<TValue>(UserDataSlot<TValue> slot, TValue value, out bool isSet)
        {
            Debug.Assert(slot.IsAllocated);

            return EnsureSlotAllocated(UserDataSlot<TValue>.TypeIndex)
                .GetOrSet(slot.ValueIndex, value, out isSet);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<TValue>(UserDataSlot<TValue> slot, TValue value)
        {
            Debug.Assert(slot.IsAllocated);

            EnsureSlotAllocated(UserDataSlot<TValue>.TypeIndex)
                .Set(slot.ValueIndex, value);
        }

        private ref BackingStorageEntry EnsureSlotAllocated(int typeIndex)
            => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(EnsureCapacity(typeIndex)), typeIndex);

        private BackingStorageEntry[] EnsureCapacity(int typeIndex)
        {
            var tablesCopy = Volatile.Read(in tables);
            if ((uint)typeIndex >= (uint)tablesCopy.Length)
                tablesCopy = Resize(typeIndex);

            return tablesCopy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Optional<TValue> Remove<TValue>(UserDataSlot<TValue> slot)
        {
            Debug.Assert(slot.IsAllocated);

            return RemoveCore(Volatile.Read(in tables), UserDataSlot<TValue>.TypeIndex, slot.ValueIndex);

            static Optional<TValue> RemoveCore(BackingStorageEntry[] tables, int typeIndex, int valueIndex)
                => (uint)typeIndex < (uint)tables.Length
                    ? tables[typeIndex].Remove<TValue>(valueIndex)
                    : Optional.None<TValue>();
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
        return Volatile.Read(in partition) ?? Interlocked.CompareExchange(ref partition, newStorage = [], null) ?? newStorage;
    }
}