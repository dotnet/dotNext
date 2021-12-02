namespace DotNext.Collections.Generic;

public static partial class Sequence
{
    private sealed class AsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        internal sealed class Enumerator : Disposable, IAsyncEnumerator<T>
        {
            private readonly IEnumerator<T> enumerator;
            private readonly CancellationToken token;

            internal Enumerator(IEnumerable<T> enumerable, CancellationToken token)
            {
                enumerator = enumerable.GetEnumerator();
                this.token = token;
            }

            public T Current => enumerator.Current;

            public ValueTask<bool> MoveNextAsync()
            {
                if (token.IsCancellationRequested)
                    return new ValueTask<bool>(Task.FromCanceled<bool>(token));

                return new ValueTask<bool>(enumerator.MoveNext());
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    enumerator.Dispose();
                }

                base.Dispose(disposing);
            }

            public new ValueTask DisposeAsync() => base.DisposeAsync();
        }

        private readonly IEnumerable<T> enumerable;

        internal AsyncEnumerable(IEnumerable<T> enumerable)
            => this.enumerable = enumerable;

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token)
            => new Enumerator(enumerable, token);
    }

    private sealed class AsyncEmptyEnumerable<T> : IAsyncEnumerable<T>
    {
        internal static readonly AsyncEmptyEnumerable<T> Instance = new();

        private AsyncEmptyEnumerable()
        {
        }

        IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken token)
            => EmptyEnumerator<T>.Instance;
    }

    /// <summary>
    /// Converts synchronous collection of elements to asynchronous.
    /// </summary>
    /// <param name="enumerable">The collection of elements.</param>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <returns>The asynchronous wrapper over synchronous collection of elements.</returns>
    public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> enumerable)
        => new AsyncEnumerable<T>(enumerable ?? throw new ArgumentNullException(nameof(enumerable)));

    /// <summary>
    /// Obtains asynchronous enumerator over the sequence of elements.
    /// </summary>
    /// <param name="enumerable">The collection of elements.</param>
    /// <param name="token">The token that can be used by consumer to cancel the enumeration.</param>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <returns>The asynchronous wrapper over synchronous enumerator.</returns>
    public static IAsyncEnumerator<T> GetAsyncEnumerator<T>(this IEnumerable<T> enumerable, CancellationToken token = default)
        => new AsyncEnumerable<T>.Enumerator(enumerable ?? throw new ArgumentNullException(nameof(enumerable)), token);

    /// <summary>
    /// Gets empty asynchronous collection.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <returns>Empty asynchronous collection.</returns>
    public static IAsyncEnumerable<T> GetEmptyAsyncEnumerable<T>() => AsyncEmptyEnumerable<T>.Instance;
}