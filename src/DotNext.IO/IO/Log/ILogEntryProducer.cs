using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNext.IO.Log
{
    /// <summary>
    /// Represents supplier of log entries.
    /// </summary>
    /// <typeparam name="TEntry">The type of the supplied log entries.</typeparam>
    public interface ILogEntryProducer<out TEntry> : IAsyncEnumerator<TEntry>
        where TEntry : notnull, ILogEntry
    {
        /// <summary>
        /// Gets the remaining count of log entries in this object.
        /// </summary>
        /// <value>The remaining count of log entries.</value>
        long RemainingCount { get; }
    }

    internal sealed class SingleEntryProducer<TEntry> : ILogEntryProducer<TEntry>
        where TEntry : notnull, ILogEntry
    {
        private bool available;

        internal SingleEntryProducer(TEntry entry)
        {
            Current = entry;
            available = true;
        }

        long ILogEntryProducer<TEntry>.RemainingCount => available.ToInt32();

        public TEntry Current { get; }

        ValueTask<bool> IAsyncEnumerator<TEntry>.MoveNextAsync()
        {
            var result = available;
            available = false;
            return new ValueTask<bool>(result);
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            available = false;
            return new ValueTask();
        }
    }

    /// <summary>
    /// Represents default implementation of <see cref="ILogEntryProducer{TEntry}"/> backed by the list
    /// of the log entries.
    /// </summary>
    /// <typeparam name="TEntry">The type of the supplied entries.</typeparam>
    public sealed class LogEntryProducer<TEntry> : ILogEntryProducer<TEntry>
        where TEntry : notnull, ILogEntry
    {
        private const int InitialPosition = -1;
        private readonly IReadOnlyList<TEntry> source;
        private int currentIndex;

        /// <summary>
        /// Initializes a new producer of the log entries passed as list.
        /// </summary>
        /// <param name="entries">The list of the log entries to be returned by the producer.</param>
        public LogEntryProducer(IReadOnlyList<TEntry> entries)
        {
            currentIndex = InitialPosition;
            source = entries;
        }

        /// <summary>
        /// Initializes a new producer of the log entries passed as array.
        /// </summary>
        /// <param name="entries">The log entries to be returned by the producer.</param>
        public LogEntryProducer(params TEntry[] entries)
            : this(entries.As<IReadOnlyList<TEntry>>())
        {
        }

        /// <summary>
        /// Initializes a new empty producer of the log entries.
        /// </summary>
        public LogEntryProducer()
            : this(Array.Empty<TEntry>())
        {
        }

        /// <inheritdoc/>
        TEntry IAsyncEnumerator<TEntry>.Current => source[currentIndex];

        /// <inheritdoc/>
        long ILogEntryProducer<TEntry>.RemainingCount => source.Count - currentIndex - 1;

        /// <inheritdoc/>
        ValueTask<bool> IAsyncEnumerator<TEntry>.MoveNextAsync()
        {
            var index = currentIndex + 1;
            bool result;
            if (result = index < source.Count)
                currentIndex = index;
            return new ValueTask<bool>(result);
        }

        /// <summary>
        /// Resets the position of the producer.
        /// </summary>
        public void Reset() => currentIndex = InitialPosition;

        /// <inheritdoc/>
        ValueTask IAsyncDisposable.DisposeAsync() => new();

        /// <summary>
        /// Constructs producer of single log entry.
        /// </summary>
        /// <param name="entry">The entry to be exposed by producer.</param>
        /// <returns>The producer of single log entry.</returns>
        public static ILogEntryProducer<TEntry> Of(TEntry entry) => new SingleEntryProducer<TEntry>(entry);
    }
}