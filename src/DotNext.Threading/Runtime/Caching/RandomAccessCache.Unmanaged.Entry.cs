using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.Caching;

using Threading;

unsafe partial class RandomAccessCache<TValue>
{
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
        private ReferenceCounter lifetimeCounter;
        private SieveState sieveState;
        private volatile byte processed; // 1 when the eviction thread processed the entry

        private Entry(int keyHashCode, int keyLength, in TValue value)
        {
            lifetimeCounter = new(initialValue: 2U);
            KeyHashCode = keyHashCode;
            KeyLength = keyLength;
            Value = value;
        }

        public bool IsProcessed
        {
            readonly get => Unsafe.BitCast<byte, bool>(processed);
            set => processed = Unsafe.BitCast<bool, byte>(value);
        }

        public readonly bool IsDead => sieveState.IsDead;

        public bool Visit() => sieveState.Visit();

        // false if the entry gets a second chance; otherwise, removed is true if the entry was evicted before
        public bool TryEvict(out bool removed) => sieveState.TryEvict(out removed);
        
        // true if the entry was alive
        public bool MarkAsEvicted() => sieveState.MarkAsEvicted();
        
        public bool TryAddRef() => lifetimeCounter.TryAdd();
        
        // the caller must own a reference
        public void AddRef() => lifetimeCounter.Add();

        public bool ReleaseRef() => lifetimeCounter.Release();

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
        if (entry->ReleaseRef())
            Retire(entry);
    }
}
