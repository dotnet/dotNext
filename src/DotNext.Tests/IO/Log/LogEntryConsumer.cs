using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.IO.Log;

/// <summary>
/// Represents unified representation of various types of log entry readers.
/// </summary>
/// <typeparam name="TEntry">The interface type of the log entries supported by audit trail.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the reader.</typeparam>
[StructLayout(LayoutKind.Auto)]
[ExcludeFromCodeCoverage]
internal readonly struct LogEntryConsumer<TEntry, TResult> : ILogEntryConsumer<TEntry, TResult>
    where TEntry : class, ILogEntry
{
    private readonly object consumer;

    /// <summary>
    /// Wraps the delegate instance as a reader of log entries.
    /// </summary>
    /// <param name="consumer">The delegate representing the reader.</param>
    public LogEntryConsumer(Func<IReadOnlyList<ILogEntry>, long?, CancellationToken, ValueTask<TResult>> consumer)
        => this.consumer = consumer;

    /// <summary>
    /// Wraps the delegate instance as a reader of log entries.
    /// </summary>
    /// <param name="consumer">The delegate representing the reader.</param>
    public LogEntryConsumer(Func<IReadOnlyList<TEntry>, long?, CancellationToken, ValueTask<TResult>> consumer)
        => this.consumer = consumer;

    /// <summary>
    /// Wraps the consumer as a reader of log entries.
    /// </summary>
    /// <param name="consumer">The consumer to be wrapped.</param>
    public LogEntryConsumer(ILogEntryConsumer<TEntry, TResult> consumer)
    {
        if (consumer is LogEntryConsumer<TEntry, TResult> typedConsumer)
        {
            this = typedConsumer;
        }
        else
        {
            this.consumer = consumer;
            LogEntryMetadataOnly = consumer.LogEntryMetadataOnly;
        }
    }

    /// <summary>
    /// Indicates that the consumer has no intention to read the content of the log entries.
    /// </summary>
    public bool LogEntryMetadataOnly { get; init; }

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
            null => ValueTask.FromException<TResult>(new NotSupportedException()),
            Func<IReadOnlyList<ILogEntry>, long?, CancellationToken, ValueTask<TResult>> func => func(new LogEntryList<ILogEntry, TEntryImpl, TList>(entries), snapshotIndex, token),
            Func<IReadOnlyList<TEntry>, long?, CancellationToken, ValueTask<TResult>> func => func(new LogEntryList<TEntry, TEntryImpl, TList>(entries), snapshotIndex, token),
            _ => Unsafe.As<ILogEntryConsumer<TEntry, TResult>>(consumer).ReadAsync<TEntryImpl, TList>(entries, snapshotIndex, token),
        };
}