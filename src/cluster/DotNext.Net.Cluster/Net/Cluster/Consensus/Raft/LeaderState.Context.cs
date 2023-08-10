using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

internal partial class LeaderState<TMember>
{
    private sealed class ContextEntry : Disposable
    {
        private int hashCode;
        internal ContextEntry? Next;
        private DependentHandle handle;

        internal ContextEntry(TMember member, int hashCode, Func<TMember, ReplicationWorkItem> factory, out ReplicationWorkItem result)
        {
            handle = new(member, result = factory.Invoke(member));
            this.hashCode = hashCode;
        }

        internal int HashCode => hashCode;

        [DisallowNull]
        internal TMember? Key
        {
            get => Unsafe.As<TMember>(handle.Target);
        }

        [DisallowNull]
        internal ReplicationWorkItem? Value
        {
            get => Unsafe.As<ReplicationWorkItem>(handle.Dependent);
        }

        internal void Reuse(TMember key, int hashCode, Func<TMember, ReplicationWorkItem> factory, out ReplicationWorkItem result)
        {
            handle.Target = key;
            handle.Dependent = result = factory.Invoke(key);
            this.hashCode = hashCode;
        }

        internal void Deconstruct(out TMember? key, out Replicator? context)
        {
            var pair = handle.TargetAndDependent;
            key = Unsafe.As<TMember>(pair.Target);
            context = Unsafe.As<Replicator>(pair.Dependent);
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
        private static readonly int HalfMaxSize = Array.MaxLength >> 1;
        private ContextEntry?[] entries;

        public Context(int sizeHint)
        {
            Debug.Assert(sizeHint > 0);

            entries = new ContextEntry?[sizeHint <= HalfMaxSize ? sizeHint << 1 : sizeHint];
        }

        public Context() => entries = Array.Empty<ContextEntry?>();

        private static int GetIndex(int hashCode, int boundary)
            => (hashCode & int.MaxValue) % boundary;

        private readonly int GetIndex(TMember member, out int hashCode)
            => GetIndex(hashCode = RuntimeHelpers.GetHashCode(member), entries.Length);

        private readonly ref ContextEntry? GetEntry(TMember member, out int hashCode)
            => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), GetIndex(member, out hashCode));

        private void ResizeAndRemoveDeadEntries()
        {
            if (entries.Length == Array.MaxLength)
                throw new OutOfMemoryException();

            var oldEntries = entries;
            entries = new ContextEntry?[entries.Length <= HalfMaxSize ? entries.Length << 1 : entries.Length + 1];

            // copy elements from old array to a new one
            for (var i = 0; i < oldEntries.Length; i++)
            {
                ref var oldEntry = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(oldEntries), i);
                for (ContextEntry? current = oldEntry, next; current is not null; current = next)
                {
                    next = current.Next; // make a copy because Next can be modified by Insert operation

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

        private readonly void Insert(ContextEntry entry)
        {
            ref var location = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(entries), GetIndex(entry.HashCode, entries.Length));

            while (location is not null)
                location = ref location.Next;

            location = entry;
        }

        public ReplicationWorkItem GetOrCreate(TMember key, Func<TMember, ReplicationWorkItem> factory)
        {
            Debug.Assert(key is not null);

            ref var entry = ref GetEntry(key, out var hashCode);
            ReplicationWorkItem? result;

            if (entry is null)
            {
                // add a new element
                entry = new(key, hashCode, factory, out result);
            }
            else
            {
                ContextEntry? entryToReuse = null;

                // try to get from dictionary
                for (var current = entry; current is not null; current = current.Next)
                {
                    var tmp = current.Key;
                    if (tmp is null)
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
                else
                {
                    // failed to reuse, add a new element
                    ResizeAndRemoveDeadEntries();
                    Insert(new(key, hashCode, factory, out result));
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

            entries = Array.Empty<ContextEntry?>();
        }
    }

    private Context context;
}