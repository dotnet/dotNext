namespace DotNext.IO.Log;

using Patterns;

/// <summary>
/// Represents supplier of log entries.
/// </summary>
/// <typeparam name="TEntry">The type of the supplied log entries.</typeparam>
public interface ILogEntryProducer<out TEntry> : IAsyncEnumerator<TEntry>
    where TEntry : ILogEntry
{
    /// <summary>
    /// Gets the remaining count of log entries in this object.
    /// </summary>
    /// <value>The remaining count of log entries.</value>
    long RemainingCount { get; }

    /// <summary>
    /// Gets the empty collection of the log entries.
    /// </summary>
    public static ILogEntryProducer<TEntry> Empty => EmptyLogEntryProducer<TEntry>.Instance;
}

file sealed class EmptyLogEntryProducer<TEntry> : ILogEntryProducer<TEntry>, ISingleton<EmptyLogEntryProducer<TEntry>>
    where TEntry : ILogEntry
{
    public static EmptyLogEntryProducer<TEntry> Instance { get; } = new();

    private EmptyLogEntryProducer()
    {
    }

    ValueTask IAsyncDisposable.DisposeAsync() => ValueTask.CompletedTask;

    ValueTask<bool> IAsyncEnumerator<TEntry>.MoveNextAsync() => ValueTask.FromResult(false);

    TEntry IAsyncEnumerator<TEntry>.Current => throw new InvalidOperationException();

    long ILogEntryProducer<TEntry>.RemainingCount => 0L;
}