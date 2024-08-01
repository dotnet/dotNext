using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using static Numerics.Number;

internal partial class LeaderState<TMember>
{
    private sealed class ContextEntry : Disposable
    {
        private int hashCode;
        internal ContextEntry? Next;
        private DependentHandle handle;

        internal ContextEntry(TMember member, int hashCode, Func<TMember, Replicator> factory, out Replicator result)
        {
            handle = new(member, result = factory.Invoke(member));
            this.hashCode = hashCode;
        }

        internal int HashCode => hashCode;

        internal TMember? Key
        {
            get => Unsafe.As<TMember>(handle.Target);
        }

        internal Replicator? Value
        {
            get => Unsafe.As<Replicator>(handle.Dependent);
        }

        internal void Reuse(TMember key, int hashCode, Func<TMember, Replicator> factory, out Replicator result)
        {
            handle.Target = key;
            handle.Dependent = result = factory.Invoke(key);
            this.hashCode = hashCode;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Next = null;
            }

            handle.Dispose();
            base.Dispose(disposing);
        }
    }

    // lightweight version of ConditionalWeakTable but without thread safety and extra methods
    [StructLayout(LayoutKind.Auto)]
#if DEBUG
    internal
#else
    private
#endif
    struct Context : IDisposable
    {
        private static ReadOnlySpan<int> Primes => [
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
            1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
            17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
            187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
            1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369];

        private ContextEntry?[] entries;
        private ulong fastModMultiplier;

        public Context(int sizeHint)
        {
            Debug.Assert(sizeHint > 0);

            var size = GetPrime(sizeHint, Primes);
            fastModMultiplier = UIntPtr.Size is sizeof(ulong) ? GetFastModMultiplier((uint)size) : default;
            entries = new ContextEntry?[size];
        }

        private static ulong GetFastModMultiplier(ulong divisor)
            => ulong.MaxValue / divisor + 1UL;

        // Daniel Lemire's fastmod algorithm
        private static uint FastMod(uint value, uint divisor, ulong multiplier)
        {
            Debug.Assert(divisor <= int.MaxValue);

            var result = (uint)(((((multiplier * value) >> 32) + 1UL) * divisor) >> 32);
            Debug.Assert(result == value % divisor);

            return result;
        }

        private static int Grow(int size, out ulong multiplier)
        {
            // This is the maximum prime smaller than Array.MaxLength
            const int maxPrimeLength = 0x7FFFFFC3;

            int newSize;
            newSize = size is maxPrimeLength
                ? throw new InsufficientMemoryException()
                : (uint)(newSize = size << 1) > maxPrimeLength && maxPrimeLength > size
                    ? maxPrimeLength
                    : GetPrime(newSize, Primes);

            multiplier = IntPtr.Size is sizeof(ulong) ? GetFastModMultiplier((uint)newSize) : default;
            return newSize;
        }

        public Context() => entries = [];

        private readonly int GetIndex(int hashCode)
            => (int)(IntPtr.Size is sizeof(ulong)
                ? FastMod((uint)hashCode, (uint)entries.Length, fastModMultiplier)
                : (uint)hashCode % (uint)entries.Length);

        private readonly int GetIndex(TMember member, out int hashCode)
            => GetIndex(hashCode = RuntimeHelpers.GetHashCode(member));

        private readonly ref ContextEntry? GetEntry(TMember member, out int hashCode)
            => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), GetIndex(member, out hashCode));

        private void ResizeAndRemoveDeadEntries(CancellationToken token)
        {
            var oldEntries = entries;
            entries = new ContextEntry?[Grow(oldEntries.Length, out fastModMultiplier)];

            // copy elements from old array to a new one
            for (var i = 0; i < oldEntries.Length; i++, token.ThrowIfCancellationRequested())
            {
                ref var oldEntry = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(oldEntries), i);
                for (ContextEntry? current = oldEntry, next; current is not null; current = next)
                {
                    next = current.Next;
                    current.Next = null;

                    if (current.Key is { } key)
                    {
                        Insert(current);
                        GC.KeepAlive(key);
                    }
                    else
                    {
                        // do not migrate dead entries
                        current.Dispose();
                    }
                }

                // help GC
                oldEntry = null;
            }
        }

        private readonly bool Insert(ContextEntry entry)
        {
            Debug.Assert(entry.Next is null);

            const int maxCollisions = 3;
            ref var location = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), GetIndex(entry.HashCode));

            int collisions;
            for (collisions = 0; location is not null; collisions++)
                location = ref location.Next;

            location = entry;
            return collisions <= maxCollisions;
        }

        public Replicator GetOrCreate(TMember key, Func<TMember, Replicator> factory, CancellationToken token = default)
        {
            Debug.Assert(key is not null);

            ref var entry = ref GetEntry(key, out var hashCode);
            Replicator? result;

            if (entry is null)
            {
                // add a new element
                entry = new(key, hashCode, factory, out result);
            }
            else
            {
                ContextEntry? entryToReuse = null;

                // try to get from dictionary
                for (var current = entry; current is not null; current = current.Next, token.ThrowIfCancellationRequested())
                {
                    if (current.Key is not { } tmp)
                    {
                        entryToReuse ??= current;
                    }
                    else if (ReferenceEquals(tmp, key))
                    {
                        // found matching entry
                        Debug.Assert(current.Value is not null);
                        Debug.Assert(hashCode == current.HashCode);

                        result = current.Value;
                        goto exit;
                    }
                }

                // try to reuse available handle
                if (entryToReuse is not null)
                {
                    entryToReuse.Reuse(key, hashCode, factory, out result);
                }
                else if (!Insert(new(key, hashCode, factory, out result)))
                {
                    // too many collisions, resize
                    ResizeAndRemoveDeadEntries(token);
                }
            }

        exit:
            GC.KeepAlive(key);
            return result;
        }

        public void Dispose()
        {
            // dispose all handles
            for (var i = 0; i < entries.Length; i++)
            {
                ref var entry = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), i);
                for (ContextEntry? current = entry, next; current is not null; current = next)
                {
                    next = current.Next;
                    current.Dispose();
                }

                entry = null;
            }

            entries = [];
        }
    }

    private Context context;
}