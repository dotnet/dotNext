using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.Caching;

using Threading;

unsafe partial class RandomAccessCache<TValue>
{
    private const sbyte NotVisited = 0;
    private const sbyte Visited = 1;
    private const sbyte Evicted = -1;

    // The entry is a single block of unmanaged memory: the header with the value, and the key bytes.
    // The runtime packs the fields to minimize padding, the code must not depend on their offsets
    [StructLayout(LayoutKind.Auto)]
    internal struct Entry
    {
        internal readonly int KeyHashCode;
        internal readonly int KeyLength;
        internal readonly TValue Value;
        
        // bucket chain, modified under the bucket lock only, traversed by the lock-free readers
        internal Entry* NextInBucket;

        // SIEVE list owned by the eviction thread. When the entry leaves the list,
        // SieveNext is reused by the epoch to link the retired entries
        internal Entry* SievePrevious, SieveNext;

        // The owners of the entry: the bucket chain, the queue commands, the SIEVE list and the sessions.
        // The entry is retired when the counter reaches zero, and reclaimed when the readers leave the epoch
        private volatile uint lifetimeCounter;
        private volatile sbyte cacheState;
        private volatile byte processed; // 1 when the eviction thread processed the entry

        private Entry(int keyHashCode, int keyLength, in TValue value)
        {
            lifetimeCounter = 2U;
            KeyHashCode = keyHashCode;
            KeyLength = keyLength;
            Value = value;
        }

        public bool IsProcessed
        {
            get => Unsafe.BitCast<byte, bool>(processed);
            set => processed = Unsafe.BitCast<bool, byte>(value);
        }

        public readonly bool IsDead => cacheState is Evicted;

        public bool Visit() => cacheState switch
        {
            Visited => true,
            NotVisited => Interlocked.CompareExchange(ref cacheState, Visited, NotVisited) is not Evicted,
            _ => false,
        };

        public bool Unvisit() => cacheState is Visited
                                 && Interlocked.CompareExchange(ref cacheState, NotVisited, Visited) is Visited;

        public bool TryAcquire()
        {
            for (uint current = lifetimeCounter, tmp; current > 0; current = tmp)
            {
                tmp = Interlocked.CompareExchange(ref lifetimeCounter, current + 1, current);
                if (tmp == current)
                    return true;
            }

            return false;
        }
        
        // true if the entry was alive
        public bool MarkAsEvicted()
            => Interlocked.Exchange(ref cacheState, Evicted) is not Evicted;
        
        // the caller must own a reference
        public void AddReference()
        {
            Debug.Assert(lifetimeCounter > 0U);

            Interlocked.Increment(ref lifetimeCounter);
        }

        public bool ReleaseReference()
        {
            Debug.Assert(lifetimeCounter > 0U);

            return Interlocked.Decrement(ref lifetimeCounter) is 0U;
        }

        public readonly long Size
            => sizeof(Entry) + (long)KeyLength;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public readonly Span<byte> Key
        {
            get
            {
                ref var reference = ref Unsafe.Add(ref Unsafe.AsRef(in this), 1);
                return MemoryMarshal.CreateSpan(ref Unsafe.As<Entry, byte>(ref reference), KeyLength);
            }
        }

        public readonly bool Matches(ReadOnlySpan<byte> key, int hashCode, IEqualityComparer<ReadOnlySpan<byte>> comparer)
            => KeyHashCode == hashCode && comparer.Equals(key, Key);

        public static Entry* Allocate(ReadOnlySpan<byte> key, int hashCode, in TValue value)
        {
            var entry = (Entry*)NativeMemory.Alloc((nuint)(uint)sizeof(Entry) + (uint)key.Length);
            Unsafe.AsRef<Entry>(entry) = new(hashCode, key.Length, in value);
            key.CopyTo(entry->Key);

            return entry;
        }

        public static void Free(Entry* entry)
        {
            Poison(entry);
            NativeMemory.Free(entry);

            // poison the reclaimed memory to detect use-after-free in tests
            [Conditional("DEBUG")]
            static void Poison(Entry* entry)
                => NativeMemory.Fill(entry, (uint)sizeof(Entry) + (uint)entry->KeyLength, 0xDD);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private struct Bucket
    {
        internal AsyncParkingLot.State LockState; // modified by AsyncParkingLot only
        internal Entry* First;
        internal Entry* Added; // the entry inserted by the session holding the lock
    }

    private void Free(Entry* entry)
    {
        OnReleased(entry);
        Entry.Free(entry);
    }

    private void Release(Entry* entry)
    {
        if (entry->ReleaseReference())
            Retire(entry);
    }
}
