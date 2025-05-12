using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Numerics;

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

        internal TMember? Key => Unsafe.As<TMember>(handle.Target);

        internal Replicator? Value => Unsafe.As<Replicator>(handle.Dependent);

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
        private ContextEntry?[] entries;
        private FastMod fastMod;

        public Context(int sizeHint)
        {
            Debug.Assert(sizeHint > 0);

            var size = PrimeNumber.GetPrime(sizeHint);
            fastMod = new((uint)size);
            entries = new ContextEntry?[size];
        }

        private static int Grow(int size, out FastMod fastMod)
        {
            // This is the maximum prime smaller than Array.MaxLength
            const int maxPrimeLength = 0x7FFFFFC3;

            int newSize;
            newSize = size is maxPrimeLength
                ? throw new InsufficientMemoryException()
                : (uint)(newSize = size << 1) > maxPrimeLength && maxPrimeLength > size
                    ? maxPrimeLength
                    : PrimeNumber.GetPrime(newSize);

            fastMod = new((uint)newSize);
            return newSize;
        }

        public Context() => entries = [];

        private readonly int GetIndex(int hashCode)
            => (int)fastMod.GetRemainder((uint)hashCode);

        private readonly int GetIndex(TMember member, out int hashCode)
            => GetIndex(hashCode = RuntimeHelpers.GetHashCode(member));

        private readonly ref ContextEntry? GetEntry(TMember member, out int hashCode)
            => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), GetIndex(member, out hashCode));

        private void ResizeAndRemoveDeadEntries(CancellationToken token)
        {
            var oldEntries = entries;
            entries = new ContextEntry?[Grow(oldEntries.Length, out fastMod)];

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