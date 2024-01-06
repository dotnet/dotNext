using System.Diagnostics;

namespace DotNext.Collections.Generic;

public static partial class AsyncEnumerable
{
    private sealed class ThrowingEnumerator<T>(Exception exception) : IAsyncEnumerator<T>, IAsyncEnumerable<T>
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public T Current => throw new InvalidOperationException();

        ValueTask<bool> IAsyncEnumerator<T>.MoveNextAsync() => ValueTask.FromException<bool>(exception);

        IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken) => this;

        ValueTask IAsyncDisposable.DisposeAsync() => ValueTask.CompletedTask;
    }
}