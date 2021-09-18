namespace DotNext.IO.Log;

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