namespace DotNext.IO.Log;

/// <summary>
/// Represents producer hint that can help audit trail to optimize
/// consumption operations.
/// </summary>
[Flags]
public enum LogEntryProducerOptimizationHint
{
    /// <summary>
    /// Optimization is not applicable.
    /// </summary>
    None = 0,

    /// <summary>
    /// The producer will likely to provide log entries synchronously
    /// and <see cref="IAsyncEnumerator{T}.MoveNextAsync"/> will not suspend the caller.
    /// </summary>
    SynchronousCompletion = 1,

    /// <summary>
    /// The payload of log entries provided by the producer are ready for consumption
    /// synchronously.
    /// </summary>
    /// <remarks>
    /// This flag means that <see cref="IDataTransferObject.WriteToAsync{TWriter}(TWriter, CancellationToken)"/>
    /// is likely to be completed synchronously.
    /// </remarks>
    LogEntryPayloadAvailableImmediately = SynchronousCompletion << 1,
}