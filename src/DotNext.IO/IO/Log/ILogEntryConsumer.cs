using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO.Log
{
    /// <summary>
    /// Represents read hint that can help audit trail to optimize
    /// read operations.
    /// </summary>
    public enum LogEntryReadOptimizationHint : byte
    {
        /// <summary>
        /// Return log entry metadata and payload.
        /// </summary>
        None = 0,

        /// <summary>
        /// Return log entry metadata only.
        /// </summary>
        MetadataOnly = 1,
    }

    /// <summary>
    /// Represents the reader of the log entries.
    /// </summary>
    /// <remarks>
    /// This is an interface type instead of delegate type because it can be implemented by value type
    /// and avoid memory allocations.
    /// </remarks>
    /// <typeparam name="TEntry">The interface type of the log entries supported by audit trail.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the reader.</typeparam>
    public interface ILogEntryConsumer<in TEntry, TResult>
        where TEntry : class, ILogEntry
    {
        /// <summary>
        /// Reads log entries asynchronously.
        /// </summary>
        /// <remarks>
        /// The actual generic types for <typeparamref name="TEntryImpl"/> and <typeparamref name="TList"/>
        /// are supplied by the infrastructure automatically.
        /// Do not return <typeparamref name="TEntryImpl"/> as a part of <typeparamref name="TResult"/>
        /// because log entries are valid only during the call of this method.
        /// </remarks>
        /// <typeparam name="TEntryImpl">The actual type of the log entries in the list.</typeparam>
        /// <typeparam name="TList">The type of the list containing log entries.</typeparam>
        /// <param name="entries">The list of the log entries.</param>
        /// <param name="snapshotIndex">Non-<see langword="null"/> if the first log entry in this list is a snapshot entry that has the specific index.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The result returned by the reader.</returns>
        ValueTask<TResult> ReadAsync<TEntryImpl, TList>(TList entries, long? snapshotIndex, CancellationToken token)
            where TEntryImpl : notnull, TEntry
            where TList : notnull, IReadOnlyList<TEntryImpl>;

        /// <summary>
        /// Gets optimization hint that may be used by the audit trail to optimize the query.
        /// </summary>
        LogEntryReadOptimizationHint OptimizationHint => LogEntryReadOptimizationHint.None;
    }

    /// <summary>
    /// Represents unified representation of various types of log entry readers.
    /// </summary>
    /// <typeparam name="TEntry">The interface type of the log entries supported by audit trail.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the reader.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct LogEntryConsumer<TEntry, TResult> : ILogEntryConsumer<TEntry, TResult>
        where TEntry : class, ILogEntry
    {
        private readonly object? consumer;

        /// <summary>
        /// Wraps the delegate instance as a reader of log entries.
        /// </summary>
        /// <param name="consumer">The delegate representing the reader.</param>
        /// <param name="optimizationHint">Represents optimization hint for the audit trail.</param>
        public LogEntryConsumer(Func<IReadOnlyList<ILogEntry>, long?, CancellationToken, ValueTask<TResult>> consumer, LogEntryReadOptimizationHint optimizationHint = LogEntryReadOptimizationHint.None)
        {
            this.consumer = consumer;
            OptimizationHint = optimizationHint;
        }

        /// <summary>
        /// Wraps the delegate instance as a reader of log entries.
        /// </summary>
        /// <param name="consumer">The delegate representing the reader.</param>
        public LogEntryConsumer(Func<IReadOnlyList<TEntry>, long?, CancellationToken, ValueTask<TResult>> consumer, LogEntryReadOptimizationHint optimizationHint = LogEntryReadOptimizationHint.None)
        {
            this.consumer = consumer;
            OptimizationHint = optimizationHint;
        }

        /// <summary>
        /// Wraps the consumer as a reader of log entries.
        /// </summary>
        /// <param name="consumer">The consumer to be wrapped.</param>
        public LogEntryConsumer(ILogEntryConsumer<TEntry, TResult> consumer)
        {
            this.consumer = consumer;
            OptimizationHint = consumer.OptimizationHint;
        }

        /// <summary>
        /// Gets optimization hint that may be used by the audit trail to optimize the query.
        /// </summary>
        public LogEntryReadOptimizationHint OptimizationHint { get; }

        /// <summary>
        /// Reads log entries asynchronously.
        /// </summary>
        /// <typeparam name="TEntryImpl">The actual type of the log entries in the list.</typeparam>
        /// <typeparam name="TList">The type of the list containing log entries.</typeparam>
        /// <param name="entries">The list of the log entries.</param>
        /// <param name="snapshotIndex">Non-<see langword="null"/> if the first log entry in this list is a snapshot entry that has the specific index.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The result returned by the reader.</returns>
        public ValueTask<TResult> ReadAsync<TEntryImpl, TList>(TList entries, long? snapshotIndex, CancellationToken token)
            where TEntryImpl : notnull, TEntry
            where TList : notnull, IReadOnlyList<TEntryImpl>
            => consumer switch
            {
                Func<IReadOnlyList<ILogEntry>, long?, CancellationToken, ValueTask<TResult>> func => func(new LogEntryList<ILogEntry, TEntryImpl, TList>(entries), snapshotIndex, token),
                Func<IReadOnlyList<TEntry>, long?, CancellationToken, ValueTask<TResult>> func => func(new LogEntryList<TEntry, TEntryImpl, TList>(entries), snapshotIndex, token),
                ILogEntryConsumer<TEntry, TResult> c => c.ReadAsync<TEntryImpl, TList>(entries, snapshotIndex, token),
#if NETSTANDARD2_1
                _ => new ValueTask<TResult>(token.IsCancellationRequested ? Task.FromCanceled<TResult>(token) : Task.FromException<TResult>(new NotSupportedException(ExceptionMessages.NoConsumerProvided)))
#else
                _ => token.IsCancellationRequested ? ValueTask.FromCanceled<TResult>(token) : ValueTask.FromException<TResult>(new NotSupportedException(ExceptionMessages.NoConsumerProvided))
#endif
            };
    }
}
