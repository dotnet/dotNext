using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Collections.Generic
{
    using Buffers;

    public static partial class Sequence
    {
        /// <summary>
        /// Applies specified action to each collection element asynchronously.
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection.</typeparam>
        /// <param name="collection">A collection to enumerate. Cannot be <see langword="null"/>.</param>
        /// <param name="action">An action to applied for each element.</param>
        /// <param name="token">The token that can be used to cancel the enumeration.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="OperationCanceledException">The enumeration has been canceled.</exception>
        public static async ValueTask ForEachAsync<T>(this IAsyncEnumerable<T> collection, ValueAction<T> action, CancellationToken token = default)
        {
            await foreach (var item in collection.WithCancellation(token))
                action.Invoke(item);
        }

        /// <summary>
        /// Applies specified action to each collection element asynchronously.
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection.</typeparam>
        /// <param name="collection">A collection to enumerate. Cannot be <see langword="null"/>.</param>
        /// <param name="action">An action to applied for each element.</param>
        /// <param name="token">The token that can be used to cancel the enumeration.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="OperationCanceledException">The enumeration has been canceled.</exception>
        public static ValueTask ForEachAsync<T>(this IAsyncEnumerable<T> collection, Action<T> action, CancellationToken token = default)
            => ForEachAsync(collection, new ValueAction<T>(action, true), token);

        /// <summary>
        /// Applies specified action to each collection element asynchronously.
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection.</typeparam>
        /// <param name="collection">A collection to enumerate. Cannot be <see langword="null"/>.</param>
        /// <param name="action">An action to applied for each element.</param>
        /// <param name="token">The token that can be used to cancel the enumeration.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="OperationCanceledException">The enumeration has been canceled.</exception>
        public static async ValueTask ForEachAsync<T>(this IAsyncEnumerable<T> collection, ValueFunc<T, CancellationToken, ValueTask> action, CancellationToken token = default)
        {
            await foreach (var item in collection.WithCancellation(token))
                await action.Invoke(item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Applies specified action to each collection element asynchronously.
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection.</typeparam>
        /// <param name="collection">A collection to enumerate. Cannot be <see langword="null"/>.</param>
        /// <param name="action">An action to applied for each element.</param>
        /// <param name="token">The token that can be used to cancel the enumeration.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="OperationCanceledException">The enumeration has been canceled.</exception>
        public static ValueTask ForEachAsync<T>(this IAsyncEnumerable<T> collection, Func<T, CancellationToken, ValueTask> action, CancellationToken token = default)
            => ForEachAsync(collection, new ValueFunc<T, CancellationToken, ValueTask>(action, true), token);

        /// <summary>
        /// Obtains first value type in the sequence; or <see langword="null"/>
        /// if sequence is empty.
        /// </summary>
        /// <typeparam name="T">Type of elements in the sequence.</typeparam>
        /// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
        /// <param name="token">The token that can be used to cancel enumeration.</param>
        /// <returns>First element in the sequence; or <see langword="null"/> if sequence is empty. </returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask<T?> FirstOrNullAsync<T>(this IAsyncEnumerable<T> seq, CancellationToken token = default)
            where T : struct
        {
            await using var enumerator = seq.GetAsyncEnumerator(token);
            return await enumerator.MoveNextAsync().ConfigureAwait(false) ? enumerator.Current : new T?();
        }

        /// <summary>
        /// Obtains first value in the sequence; or <see cref="Optional{T}.None"/>
        /// if sequence is empty.
        /// </summary>
        /// <typeparam name="T">Type of elements in the sequence.</typeparam>
        /// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
        /// <param name="token">The token that can be used to cancel enumeration.</param>
        /// <returns>The first element in the sequence; or <see cref="Optional{T}.None"/> if sequence is empty. </returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask<Optional<T>> FirstOrEmptyAsync<T>(this IAsyncEnumerable<T> seq, CancellationToken token = default)
        {
            await using var enumerator = seq.GetAsyncEnumerator(token);
            return await enumerator.MoveNextAsync().ConfigureAwait(false) ? enumerator.Current : Optional<T>.None;
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
        public static async ValueTask<Optional<T>> FirstOrEmptyAsync<T>(this IAsyncEnumerable<T> seq, ValueFunc<T, bool> filter, CancellationToken token = default)
            where T : notnull
        {
            await foreach (var item in seq.WithCancellation(token))
            {
                if (filter.Invoke(item))
                    return item;
            }

            return Optional<T>.None;
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
        public static ValueTask<Optional<T>> FirstOrEmptyAsync<T>(this IAsyncEnumerable<T> seq, Predicate<T> filter, CancellationToken token = default)
            where T : notnull
            => FirstOrEmptyAsync(seq, filter.AsValueFunc(true), token);

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
            while (count > 0)
            {
                if (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    count--;
                else
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
            await using (var enumerator = collection.GetAsyncEnumerator(token))
            {
                await enumerator.SkipAsync(index).ConfigureAwait(false);
                return await enumerator.MoveNextAsync().ConfigureAwait(false) ?
                    enumerator.Current :
                    Optional<T>.None;
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
            => new AsyncNotNullEnumerable<T>(collection);

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
            using var buffer = new PooledBufferWriter<T>(allocator, initialCapacity);

            await foreach (var item in collection.WithCancellation(token))
            {
                buffer.Add(item);
            }

            return buffer.WrittenMemory.ToArray();
        }
    }
}