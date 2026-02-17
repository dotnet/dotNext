namespace DotNext.Collections.Generic;

/// <summary>
/// Provides extension methods for <see cref="IAsyncEnumerable{T}"/> interface.
/// </summary>
public static partial class AsyncEnumerable
{
    extension<T>(IAsyncEnumerable<T> collection)
        where T : allows ref struct
    {
        /// <summary>
        /// Applies specified action to each element of the collection asynchronously.
        /// </summary>
        /// <param name="action">An action to applied for each element.</param>
        /// <param name="token">The token that can be used to cancel the enumeration.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="OperationCanceledException">The enumeration has been canceled.</exception>
        public async ValueTask ForEachAsync(Action<T> action, CancellationToken token = default)
        {
            await foreach (var item in collection.WithCancellation(token).ConfigureAwait(false))
                action(item);
        }
        
        /// <summary>
        /// Applies the specified action to each element of the collection asynchronously.
        /// </summary>
        /// <param name="action">An action to applied for each element.</param>
        /// <param name="token">The token that can be used to cancel the enumeration.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="OperationCanceledException">The enumeration has been canceled.</exception>
        public async ValueTask ForEachAsync(Func<T, CancellationToken, ValueTask> action, CancellationToken token = default)
        {
            await foreach (var item in collection.WithCancellation(token).ConfigureAwait(false))
                await action(item, token).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Gets an asynchronous collection that throws the specified exception.
        /// </summary>
        /// <param name="e">The exception to be thrown by the enumerator.</param>
        /// <returns>Empty asynchronous collection which enumerator throws <paramref name="e"/>.</returns>
        public static IAsyncEnumerable<T> Throw(Exception e)
        {
            ArgumentNullException.ThrowIfNull(e);

            return new ThrowingEnumerator<T>(e);
        }
    }
    
    /// <param name="collection">A collection to enumerate. Cannot be <see langword="null"/>.</param>
    /// <typeparam name="T">Type of elements in the collection.</typeparam>
    extension<T>(IAsyncEnumerable<T> collection)
    {
        /// <summary>
        /// Obtains the first element of a sequence; or <see cref="Optional{T}.None"/>
        /// if the sequence is empty.
        /// </summary>
        /// <param name="token">The token that can be used to cancel enumeration.</param>
        /// <returns>The first element in the sequence; or <see cref="Optional{T}.None"/> if sequence is empty. </returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public async ValueTask<Optional<T>> FirstOrNoneAsync(CancellationToken token = default)
        {
            var enumerator = collection.GetAsyncEnumerator(token);
            await using (enumerator.ConfigureAwait(false))
                return await enumerator.MoveNextAsync().ConfigureAwait(false) ? enumerator.Current : Optional<T>.None;
        }

        /// <summary>
        /// Obtains the last element of a sequence; or <see cref="Optional{T}.None"/>
        /// if the sequence is empty.
        /// </summary>
        /// <param name="token">The token that can be used to cancel enumeration.</param>
        /// <returns>The last element in the sequence; or <see cref="Optional{T}.None"/> if sequence is empty. </returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public async ValueTask<Optional<T>> LastOrNoneAsync(CancellationToken token = default)
        {
            var result = Optional<T>.None;
            await foreach (var item in collection.WithCancellation(token).ConfigureAwait(false))
                result = item;

            return result;
        }

        /// <summary>
        /// Returns the first element in a sequence that satisfies a specified condition.
        /// </summary>
        /// <param name="filter">A function to test each element for a condition.</param>
        /// <param name="token">The token that can be used to cancel enumeration.</param>
        /// <returns>The first element in the sequence that matches to the specified filter; or empty value.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public async ValueTask<Optional<T>> FirstOrNoneAsync(Predicate<T> filter, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(filter);
        
            await foreach (var item in collection.WithCancellation(token).ConfigureAwait(false))
            {
                if (filter(item))
                    return item;
            }

            return Optional<T>.None;
        }
    }

    /// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
    /// <typeparam name="T">Type of elements in the sequence.</typeparam>
    extension<T>(IAsyncEnumerable<T> seq) where T : struct
    {
        /// <summary>
        /// Obtains the first value of a sequence; or <see langword="null"/>
        /// if the sequence is empty.
        /// </summary>
        /// <param name="token">The token that can be used to cancel enumeration.</param>
        /// <returns>First element in the sequence; or <see langword="null"/> if sequence is empty. </returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public async ValueTask<T?> FirstOrNullAsync(CancellationToken token = default)
        {
            var enumerator = seq.GetAsyncEnumerator(token);
            await using (enumerator.ConfigureAwait(false))
                return await enumerator.MoveNextAsync().ConfigureAwait(false) ? enumerator.Current : null;
        }

        /// <summary>
        /// Obtains the last value of a sequence; or <see langword="null"/>
        /// if the sequence is empty.
        /// </summary>
        /// <param name="token">The token that can be used to cancel enumeration.</param>
        /// <returns>The last element in the sequence; or <see langword="null"/> if sequence is empty. </returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public async ValueTask<T?> LastOrNullAsync(CancellationToken token = default)
        {
            T? result = null;
            await foreach (var item in seq.WithCancellation(token).ConfigureAwait(false))
                result = item;

            return result;
        }
    }

    /// <summary>
    /// Skip <see langword="null"/> values in the collection.
    /// </summary>
    /// <typeparam name="T">Type of elements in the collection.</typeparam>
    /// <param name="collection">A collection to check. Cannot be <see langword="null"/>.</param>
    /// <returns>Modified lazy collection without <see langword="null"/> values.</returns>
    public static IAsyncEnumerable<T> SkipNulls<T>(this IAsyncEnumerable<T?> collection)
        where T : class
        => new NotNullEnumerable<T>(collection);

    /// <summary>
    /// Extends <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    /// <typeparam name="T">Type of list items.</typeparam>
    extension<T>(IAsyncEnumerable<T>)
    {
        /// <summary>
        /// Constructs read-only sequence with a single item in it.
        /// </summary>
        /// <param name="item">An item to be placed into list.</param>
        /// <returns>Read-only list containing single item.</returns>
        public static IAsyncEnumerable<T> Singleton(T item)
            => new Specialized.SingletonList<T> { Item = item };
    }
}