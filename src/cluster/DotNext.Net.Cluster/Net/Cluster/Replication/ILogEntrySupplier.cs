using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Replication
{
    /// <summary>
    /// Represents supplier of log entries.
    /// </summary>
    /// <typeparam name="TEntry">The type of the supplied log entries.</typeparam>
    public interface ILogEntryProducer<TEntry>  //TODO: Should be inherited from IAsyncEnumerator in .NET Standard 2.1
        where TEntry : ILogEntry
    {
        /// <summary>
        /// Gets the remaining count of log entries in this object.
        /// </summary>
        /// <value>The remaining count of log entries.</value>
        long RemainingCount { get; }

        /// <summary>
        /// Gets the log entry at the current position of the enumerator.
        /// </summary>
        /// <value>The log entry at the current position of the enumerator.</value>
        TEntry Current { get; }
        
        /// <summary>
        /// Advances position of the enumerator to the next available log entry.
        /// </summary>
        /// <returns><see langword="true"/> if the enumerator advances to the next log entry; <see langword="false"/> if the enumerator reaches the end of the collection.</returns>
        ValueTask<bool> MoveNextAsync();

        /// <summary>
        /// Provides batch read of the log entries.
        /// </summary>
        /// <param name="entries">The memory used to store the log entries.</param>
        /// <returns>The actual number of copied log entries.</returns>
        ValueTask<long> ReadAsync(Memory<TEntry> entries);  //TODO: Should have default implementation in C# 8
    }

    /// <summary>
    /// Represents default implementation of <see cref="ILogEntryProducer{TEntry}"/> backed by the list
    /// of the log entries.
    /// </summary>
    /// <typeparam name="TEntry">The type of the entries supplied by this</typeparam>
    public sealed class LogEntryProducer<TEntry> : ILogEntryProducer<TEntry>
        where TEntry : ILogEntry
    {
        private const int InitialPosition = -1;
        private int currentIndex;
        private readonly IList<TEntry> source;

        /// <summary>
        /// Initializes a new producer of the log entries passed as list.
        /// </summary>
        /// <param name="entries">The list of the log entries to be returned by the producer.</param>
        public LogEntryProducer(IList<TEntry> entries)
        {
            currentIndex = InitialPosition;
            this.source = entries;
        }

        /// <summary>
        /// Initializes a new producer of the log entries passed as array.
        /// </summary>
        /// <param name="entries">The log entries to be returned by the producer.</param>
        public LogEntryProducer(params TEntry[] entries)
            : this((IList<TEntry>)entries)
        {
        }

        /// <summary>
        /// Initializes a new empty producer of the log entries.
        /// </summary>
        public LogEntryProducer()
            : this(Array.Empty<TEntry>())
        {
        }

        TEntry ILogEntryProducer<TEntry>.Current => source[currentIndex];

        long ILogEntryProducer<TEntry>.RemainingCount => source.Count - currentIndex + 1;

        ValueTask<bool> ILogEntryProducer<TEntry>.MoveNextAsync() => new ValueTask<bool>(currentIndex++ < source.Count);

        /// <summary>
        /// Resets the position of the producer.
        /// </summary>
        public void Reset() => currentIndex = InitialPosition;

        ValueTask<long> ILogEntryProducer<TEntry>.ReadAsync(Memory<TEntry> entries)
        {
            int offset;
            for(offset = 0; offset < entries.Length && ++currentIndex < source.Count; offset++)
                entries.Span[offset] = source[currentIndex];
            return new ValueTask<long>(offset);
        }
    }
}