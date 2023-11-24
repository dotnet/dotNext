using System.Runtime.CompilerServices;

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

    long ILogEntryProducer<TEntry>.RemainingCount => Unsafe.BitCast<bool, byte>(available);

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