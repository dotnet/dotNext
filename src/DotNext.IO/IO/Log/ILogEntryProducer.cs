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
    /// The payload of log entries provided by the producer are ready for consumption
    /// synchronously.
    /// </summary>
    /// <remarks>
    /// This flag means that <see cref="IDataTransferObject.WriteToAsync{TWriter}(TWriter, CancellationToken)"/>
    /// is likely to be completed synchronously.
    /// </remarks>
    bool LogEntryPayloadAvailableImmediately => false;
}