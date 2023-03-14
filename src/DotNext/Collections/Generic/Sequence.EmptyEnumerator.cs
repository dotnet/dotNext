using System.Collections;
using System.Diagnostics;

namespace DotNext.Collections.Generic;

public static partial class Sequence
{
    [DebuggerDisplay("Count = 0")]
    private sealed class EmptyEnumerator<T> : IEnumerator<T>, IAsyncEnumerator<T>, IAsyncEnumerable<T>
    {
        internal static readonly EmptyEnumerator<T> Instance = new();

        private EmptyEnumerator()
        {
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public T Current => throw new InvalidOperationException();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        object? IEnumerator.Current => Current;

        bool IEnumerator.MoveNext() => false;

        ValueTask<bool> IAsyncEnumerator<T>.MoveNextAsync() => new(false);

        IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken) => this;

        void IEnumerator.Reset()
        {
        }

        void IDisposable.Dispose()
        {
        }

        ValueTask IAsyncDisposable.DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets empty enumerator.
    /// </summary>
    /// <typeparam name="T">The type of elements in the enumerator.</typeparam>
    /// <returns>Empty enumerator.</returns>
    public static IEnumerator<T> GetEmptyEnumerator<T>() => EmptyEnumerator<T>.Instance;
}