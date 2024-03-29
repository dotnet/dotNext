namespace DotNext.Collections.Generic;

public static partial class AsyncEnumerable
{
    private sealed class NotNullEnumerable<T> : IAsyncEnumerable<T>
        where T : class
    {
        private sealed class Enumerator : IAsyncEnumerator<T>
        {
            private readonly IAsyncEnumerator<T?> enumerator;
            private T? current;

            internal Enumerator(IAsyncEnumerable<T?> enumerable, CancellationToken token)
                => enumerator = enumerable.GetAsyncEnumerator(token);

            public T Current => current ?? throw new InvalidOperationException();

            public async ValueTask<bool> MoveNextAsync()
            {
                for (T? current; await enumerator.MoveNextAsync().ConfigureAwait(false);)
                {
                    current = enumerator.Current;
                    if (current is not null)
                    {
                        this.current = current;
                        return true;
                    }
                }

                return false;
            }

            public ValueTask DisposeAsync()
            {
                current = null;
                return enumerator.DisposeAsync();
            }
        }

        private readonly IAsyncEnumerable<T?> enumerable;

        internal NotNullEnumerable(IAsyncEnumerable<T?> enumerable)
            => this.enumerable = enumerable;

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token)
            => new Enumerator(enumerable, token);
    }
}