namespace DotNext.IO.Log;

/// <summary>
/// Represents default implementation of <see cref="ILogEntryProducer{TEntry}"/> backed by the list
/// of the log entries.
/// </summary>
/// <typeparam name="TEntry">The type of the supplied entries.</typeparam>
/// <param name="entries">The list of the log entries to be returned by the producer.</param>
public sealed class LogEntryProducer<TEntry>(IReadOnlyList<TEntry> entries) : ILogEntryProducer<TEntry>, IResettable
    where TEntry : notnull, ILogEntry
{
    private const int InitialPosition = -1;
    private int currentIndex = InitialPosition;

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
        : this([])
    {
    }

    /// <inheritdoc/>
    TEntry IAsyncEnumerator<TEntry>.Current => entries[currentIndex];

    /// <inheritdoc/>
    long ILogEntryProducer<TEntry>.RemainingCount => entries.Count - currentIndex - 1;

    /// <inheritdoc/>
    ValueTask<bool> IAsyncEnumerator<TEntry>.MoveNextAsync()
    {
        var index = currentIndex + 1;
        bool result;
        if (result = index < entries.Count)
            currentIndex = index;

        return ValueTask.FromResult(result);
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