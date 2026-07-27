namespace DotNext.Collections.Generic;

public static partial class AsyncEnumerable
{
    /// <summary>
    /// Skip <see langword="null"/> values in the collection.
    /// </summary>
    /// <typeparam name="T">Type of elements in the collection.</typeparam>
    /// <param name="collection">A collection to check. Cannot be <see langword="null"/>.</param>
    /// <returns>Modified lazy collection without <see langword="null"/> values.</returns>
    public static IAsyncEnumerable<T> SkipNulls<T>(this IAsyncEnumerable<T?> collection)
        where T : class
        => new NotNullEnumerable<T>(collection);
}

file sealed class NotNullEnumerable<T>(IAsyncEnumerable<T?> enumerable) : IAsyncEnumerable<T>
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
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                if (enumerator.Current is { } currentValue)
                {
                    current = currentValue;
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

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken token)
        => new Enumerator(enumerable, token);
}