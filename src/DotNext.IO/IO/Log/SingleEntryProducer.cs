namespace DotNext.IO.Log;

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