namespace DotNext.Collections.Generic;

public static partial class AsyncEnumerable
{
    /// <summary>
    /// Skips <see langword="null"/> values in the collection.
    /// </summary>
    /// <typeparam name="T">Type of elements in the collection.</typeparam>
    /// <param name="collection">A collection to check. Cannot be <see langword="null"/>.</param>
    /// <returns>Modified lazy collection without <see langword="null"/> values.</returns>
    public static IAsyncEnumerable<T> SkipNulls<T>(this IAsyncEnumerable<T?> collection)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(collection);
        
        return new NotNullEnumerable<T?, T, NotNullFilter<T>>(collection);
    }

    /// <summary>
    /// Skips <see langword="null"/> values in the collection.
    /// </summary>
    /// <typeparam name="T">Type of elements in the collection.</typeparam>
    /// <param name="collection">A collection to check. Cannot be <see langword="null"/>.</param>
    /// <returns>Modified lazy collection without <see langword="null"/> values.</returns>
    public static IAsyncEnumerable<T> SkipNulls<T>(this IAsyncEnumerable<T?> collection)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(collection);
        
        return new NotNullEnumerable<T?, T, HasValueFilter<T>>(collection);
    }

    /// <summary>
    /// Returns only elements which has values.
    /// </summary>
    /// <typeparam name="T">Type of the monad.</typeparam>
    /// <typeparam name="TMonad">Type of the elements in the collection.</typeparam>
    /// <param name="collection">A collection to check. Cannot be <see langword="null"/>.</param>
    /// <returns>Modified lazy collection of values.</returns>
    public static IAsyncEnumerable<T> Flatten<T, TMonad>(this IAsyncEnumerable<TMonad> collection)
        where T : notnull
        where TMonad : struct, IOptionMonad<T>
    {
        ArgumentNullException.ThrowIfNull(collection);

        return new NotNullEnumerable<TMonad, T, NotEmptyFilter<TMonad, T>>(collection);
    }
}

file sealed class NotNullEnumerable<TInput, TOutput, TFilter>(IAsyncEnumerable<TInput?> enumerable) : IAsyncEnumerable<TOutput>
    where TOutput : notnull
    where TFilter : INullabilityFilter<TInput, TOutput>, allows ref struct
{
    private sealed class Enumerator : IAsyncEnumerator<TOutput>
    {
        private readonly IAsyncEnumerator<TInput?> enumerator;
        private TOutput? current;

        internal Enumerator(IAsyncEnumerable<TInput?> enumerable, CancellationToken token)
            => enumerator = enumerable.GetAsyncEnumerator(token);

        public TOutput Current => current!;

        public async ValueTask<bool> MoveNextAsync()
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                if (TFilter.TryGet(enumerator.Current, out current))
                    return true;
            }

            return false;
        }

        public ValueTask DisposeAsync()
        {
            current = default;
            return enumerator.DisposeAsync();
        }
    }

    public IAsyncEnumerator<TOutput> GetAsyncEnumerator(CancellationToken token)
        => new Enumerator(enumerable, token);
}