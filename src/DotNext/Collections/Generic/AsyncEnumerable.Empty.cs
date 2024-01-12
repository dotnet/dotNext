using System.Diagnostics;

namespace DotNext.Collections.Generic;

using Patterns;

public static partial class AsyncEnumerable
{
    [DebuggerDisplay("Count = 0")]
    private sealed class EmptyEnumerator<T> : IAsyncEnumerator<T>, IAsyncEnumerable<T>, ISingleton<EmptyEnumerator<T>>
    {
        public static EmptyEnumerator<T> Instance { get; } = new();

        private EmptyEnumerator()
        {
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public T Current => throw new InvalidOperationException();

        ValueTask<bool> IAsyncEnumerator<T>.MoveNextAsync() => new(false);

        IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken) => this;

        ValueTask IAsyncDisposable.DisposeAsync() => ValueTask.CompletedTask;
    }
}