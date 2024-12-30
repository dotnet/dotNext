using System.Runtime.CompilerServices;

namespace DotNext.IO.Log;

internal sealed class SingleEntryProducer<TEntry>(TEntry entry) : ILogEntryProducer<TEntry>
    where TEntry : ILogEntry
{
    private bool available = true;

    long ILogEntryProducer<TEntry>.RemainingCount => Unsafe.BitCast<bool, byte>(available);

    TEntry IAsyncEnumerator<TEntry>.Current => entry;

    ValueTask<bool> IAsyncEnumerator<TEntry>.MoveNextAsync()
    {
        var result = available;
        available = false;
        return ValueTask.FromResult(result);
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        available = false;
        return ValueTask.CompletedTask;
    }
}