namespace DotNext.Collections.Generic;

using Buffers;

/// <summary>
/// Provides extension methods for <see cref="IAsyncEnumerable{T}"/> interface.
/// </summary>
public static partial class AsyncEnumerable
{
    /// <summary>
    /// Applies specified action to each element of the collection asynchronously.
    /// </summary>
    /// <typeparam name="T">Type of elements in the collection.</typeparam>
    /// <param name="collection">A collection to enumerate. Cannot be <see langword="null"/>.</param>
    /// <param name="action">An action to applied for each element.</param>
    /// <param name="token">The token that can be used to cancel the enumeration.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The enumeration has been canceled.</exception>
    public static async ValueTask ForEachAsync<T>(this IAsyncEnumerable<T> collection, Action<T> action, CancellationToken token = default)
    {
        await foreach (var item in collection.WithCancellation(token).ConfigureAwait(false))
            action(item);
    }

    /// <summary>
    /// Applies the specified action to each element of the collection asynchronously.
    /// </summary>
    /// <typeparam name="T">Type of elements in the collection.</typeparam>
    /// <param name="collection">A collection to enumerate. Cannot be <see langword="null"/>.</param>
    /// <param name="action">An action to applied for each element.</param>
    /// <param name="token">The token that can be used to cancel the enumeration.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The enumeration has been canceled.</exception>
    public static async ValueTask ForEachAsync<T>(this IAsyncEnumerable<T> collection, Func<T, CancellationToken, ValueTask> action, CancellationToken token = default)
    {
        await foreach (var item in collection.WithCancellation(token).ConfigureAwait(false))
            await action(item, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtains the first value of a sequence; or <see langword="null"/>
    /// if the sequence is empty.
    /// </summary>
    /// <typeparam name="T">Type of elements in the sequence.</typeparam>
    /// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
    /// <param name="token">The token that can be used to cancel enumeration.</param>
    /// <returns>First element in the sequence; or <see langword="null"/> if sequence is empty. </returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<T?> FirstOrNullAsync<T>(this IAsyncEnumerable<T> seq, CancellationToken token = default)
        where T : struct
    {
        var enumerator = seq.GetAsyncEnumerator(token);
        await using (enumerator.ConfigureAwait(false))
            return await enumerator.MoveNextAsync().ConfigureAwait(false) ? enumerator.Current : new T?();
    }

    /// <summary>
    /// Obtains the last value of a sequence; or <see langword="null"/>
    /// if the sequence is empty.
    /// </summary>
    /// <typeparam name="T">Type of elements in the sequence.</typeparam>
    /// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
    /// <param name="token">The token that can be used to cancel enumeration.</param>
    /// <returns>The last element in the sequence; or <see langword="null"/> if sequence is empty. </returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<T?> LastOrNullAsync<T>(this IAsyncEnumerable<T> seq, CancellationToken token = default)
        where T : struct
    {
        T? result = null;
        await foreach (var item in seq.WithCancellation(token).ConfigureAwait(false))
            result = item;

        return result;
    }

    /// <summary>
    /// Obtains the first element of a sequence; or <see cref="Optional{T}.None"/>
    /// if the sequence is empty.
    /// </summary>
    /// <typeparam name="T">Type of elements in the sequence.</typeparam>
    /// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
    /// <param name="token">The token that can be used to cancel enumeration.</param>
    /// <returns>The first element in the sequence; or <see cref="Optional{T}.None"/> if sequence is empty. </returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<Optional<T>> FirstOrNoneAsync<T>(this IAsyncEnumerable<T> seq, CancellationToken token = default)
    {
        var enumerator = seq.GetAsyncEnumerator(token);
        await using (enumerator.ConfigureAwait(false))
            return await enumerator.MoveNextAsync().ConfigureAwait(false) ? enumerator.Current : Optional<T>.None;
    }

    /// <summary>
    /// Obtains the last element of a sequence; or <see cref="Optional{T}.None"/>
    /// if the sequence is empty.
    /// </summary>
    /// <typeparam name="T">Type of elements in the sequence.</typeparam>
    /// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
    /// <param name="token">The token that can be used to cancel enumeration.</param>
    /// <returns>The last element in the sequence; or <see cref="Optional{T}.None"/> if sequence is empty. </returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<Optional<T>> LastOrNoneAsync<T>(this IAsyncEnumerable<T> seq, CancellationToken token = default)
    {
        var result = Optional<T>.None;
        await foreach (var item in seq.WithCancellation(token).ConfigureAwait(false))
            result = item;

        return result;
    }

    /// <summary>
    /// Returns the first element in a sequence that satisfies a specified condition.
    /// </summary>
    /// <typeparam name="T">The type of the elements of source.</typeparam>
    /// <param name="seq">A collection to return an element from.</param>
    /// <param name="filter">A function to test each element for a condition.</param>
    /// <param name="token">The token that can be used to cancel enumeration.</param>
    /// <returns>The first element in the sequence that matches to the specified filter; or empty value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<Optional<T>> FirstOrNoneAsync<T>(this IAsyncEnumerable<T> seq, Predicate<T> filter, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        
        await foreach (var item in seq.WithCancellation(token).ConfigureAwait(false))
        {
            if (filter(item))
                return item;
        }

        return Optional<T>.None;
    }

    /// <summary>
    /// Bypasses a specified number of elements in a sequence.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <param name="enumerator">Enumerator to modify. Cannot be <see langword="null"/>.</param>
    /// <param name="count">The number of elements to skip.</param>
    /// <returns><see langword="true"/>, if current element is available; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<bool> SkipAsync<T>(this IAsyncEnumerator<T> enumerator, int count)
    {
        for (; count > 0; count--)
        {
            if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Obtains element at the specified index in the sequence.
    /// </summary>
    /// <typeparam name="T">Type of elements in the sequence.</typeparam>
    /// <param name="collection">Source collection.</param>
    /// <param name="index">Index of the element to read.</param>
    /// <param name="token">The token that can be used to cancel enumeration.</param>
    /// <returns>The requested element; or <see cref="Optional{T}.None"/> if index is out of range.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<Optional<T>> ElementAtAsync<T>(this IAsyncEnumerable<T> collection, int index, CancellationToken token = default)
    {
        var enumerator = collection.GetAsyncEnumerator(token);
        await using (enumerator.ConfigureAwait(false))
        {
            return await enumerator.SkipAsync(index).ConfigureAwait(false) && await enumerator.MoveNextAsync().ConfigureAwait(false)
                ? enumerator.Current
                : Optional<T>.None;
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
    /// Converts asynchronous collection to the array.
    /// </summary>
    /// <param name="collection">The asynchronous collection.</param>
    /// <param name="initialCapacity">The initial capacity of internal buffer.</param>
    /// <param name="allocator">The memory allocator used by internal buffer.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="T">Type of elements in the collection.</typeparam>
    /// <returns>The array representing all elements from the source collection.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async Task<T[]> ToArrayAsync<T>(this IAsyncEnumerable<T> collection, int initialCapacity = 10, MemoryAllocator<T>? allocator = null, CancellationToken token = default)
    {
        using var buffer = new PoolingBufferWriter<T>(allocator) { Capacity = initialCapacity };

        await foreach (var item in collection.WithCancellation(token).ConfigureAwait(false))
        {
            buffer.Add(item);
        }

        return buffer.WrittenMemory.ToArray();
    }

    /// <summary>
    /// Gets an empty asynchronous collection.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <returns>Empty asynchronous collection.</returns>
    public static IAsyncEnumerable<T> Empty<T>() => EmptyEnumerator<T>.Instance;

    /// <summary>
    /// Gets an asynchronous collection that throws the specified exception.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <param name="e">The exception to be thrown by the enumerator.</param>
    /// <returns>Empty asynchronous collection which enumerator throws <paramref name="e"/>.</returns>
    public static IAsyncEnumerable<T> Throw<T>(Exception e)
    {
        ArgumentNullException.ThrowIfNull(e);

        return new ThrowingEnumerator<T>(e);
    }
}