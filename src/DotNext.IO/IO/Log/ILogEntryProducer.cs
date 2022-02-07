namespace DotNext.IO.Log;

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

    /// <summary>
    /// Gets optimization hint that may be used by the audit trail to optimize the consumption.
    /// </summary>
    LogEntryProducerOptimizationHint OptimizationHint => LogEntryProducerOptimizationHint.None;
}