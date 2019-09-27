using System;
using System.Collections;
using System.Collections.Generic;

namespace DotNext.Net.Cluster.Replication
{
    /// <summary>
    /// Represents a list with single log entry.
    /// </summary>
    /// <typeparam name="TEntry">The type of the log entry to be placed into the list.</typeparam>
    internal readonly struct SingletonEntryList<TEntry> : IReadOnlyList<TEntry>
        where TEntry : ILogEntry
    {
        private readonly TEntry entry;

        internal SingletonEntryList(TEntry entry)
        {
            this.entry = entry;
        }

        int IReadOnlyCollection<TEntry>.Count => 1;

        TEntry IReadOnlyList<TEntry>.this[int index] => index == 0 ? entry : throw new IndexOutOfRangeException();

        public IEnumerator<TEntry> GetEnumerator()
        {
            yield return entry;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
