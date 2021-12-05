using System.Collections;

namespace DotNext.Collections.Generic;

public static partial class Sequence
{
    private sealed class EmptyEnumerator<T> : IEnumerator<T>, IAsyncEnumerator<T>
    {
        internal static readonly EmptyEnumerator<T> Instance = new();

        private EmptyEnumerator()
        {
        }

        public T Current => throw new InvalidOperationException();

        object? IEnumerator.Current => Current;

        bool IEnumerator.MoveNext() => false;

        ValueTask<bool> IAsyncEnumerator<T>.MoveNextAsync() => new(false);

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