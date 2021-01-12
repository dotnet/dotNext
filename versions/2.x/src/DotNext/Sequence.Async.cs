using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext
{
    using Buffers;
    using NewSequence = Collections.Generic.Sequence;

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
        public static ValueTask ForEachAsync<T>(IAsyncEnumerable<T> collection, ValueAction<T> action, CancellationToken token = default)
            => NewSequence.ForEachAsync(collection, action, token);

        /// <summary>
        /// Applies specified action to each collection element asynchronously.
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection.</typeparam>
        /// <param name="collection">A collection to enumerate. Cannot be <see langword="null"/>.</param>
        /// <param name="action">An action to applied for each element.</param>
        /// <param name="token">The token that can be used to cancel the enumeration.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="OperationCanceledException">The enumeration has been canceled.</exception>
        public static ValueTask ForEachAsync<T>(IAsyncEnumerable<T> collection, Action<T> action, CancellationToken token = default)
            => NewSequence.ForEachAsync(collection, action, token);

        /// <summary>
        /// Applies specified action to each collection element asynchronously.
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection.</typeparam>
        /// <param name="collection">A collection to enumerate. Cannot be <see langword="null"/>.</param>
        /// <param name="action">An action to applied for each element.</param>
        /// <param name="token">The token that can be used to cancel the enumeration.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="OperationCanceledException">The enumeration has been canceled.</exception>
        public static ValueTask ForEachAsync<T>(IAsyncEnumerable<T> collection, ValueFunc<T, CancellationToken, ValueTask> action, CancellationToken token = default)
            => NewSequence.ForEachAsync(collection, action, token);

        /// <summary>
        /// Applies specified action to each collection element asynchronously.
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection.</typeparam>
        /// <param name="collection">A collection to enumerate. Cannot be <see langword="null"/>.</param>
        /// <param name="action">An action to applied for each element.</param>
        /// <param name="token">The token that can be used to cancel the enumeration.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="OperationCanceledException">The enumeration has been canceled.</exception>
        public static ValueTask ForEachAsync<T>(IAsyncEnumerable<T> collection, Func<T, CancellationToken, ValueTask> action, CancellationToken token = default)
            => NewSequence.ForEachAsync(collection, action, token);

        /// <summary>
        /// Obtains first value type in the sequence; or <see langword="null"/>
        /// if sequence is empty.
        /// </summary>
        /// <typeparam name="T">Type of elements in the sequence.</typeparam>
        /// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
        /// <param name="token">The token that can be used to cancel enumeration.</param>
        /// <returns>First element in the sequence; or <see langword="null"/> if sequence is empty. </returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<T?> FirstOrNullAsync<T>(IAsyncEnumerable<T> seq, CancellationToken token = default)
            where T : struct
            => NewSequence.FirstOrNullAsync(seq, token);

        /// <summary>
        /// Obtains first value in the sequence; or <see cref="Optional{T}.None"/>
        /// if sequence is empty.
        /// </summary>
        /// <typeparam name="T">Type of elements in the sequence.</typeparam>
        /// <param name="seq">A sequence to check. Cannot be <see langword="null"/>.</param>
        /// <param name="token">The token that can be used to cancel enumeration.</param>
        /// <returns>The first element in the sequence; or <see cref="Optional{T}.None"/> if sequence is empty. </returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<Optional<T>> FirstOrEmptyAsync<T>(IAsyncEnumerable<T> seq, CancellationToken token = default)
            => NewSequence.FirstOrEmptyAsync(seq, token);

        /// <summary>
        /// Returns the first element in a sequence that satisfies a specified condition.
        /// </summary>
        /// <typeparam name="T">The type of the elements of source.</typeparam>
        /// <param name="seq">A collection to return an element from.</param>
        /// <param name="filter">A function to test each element for a condition.</param>
        /// <param name="token">The token that can be used to cancel enumeration.</param>
        /// <returns>The first element in the sequence that matches to the specified filter; or empty value.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<Optional<T>> FirstOrEmptyAsync<T>(IAsyncEnumerable<T> seq, ValueFunc<T, bool> filter, CancellationToken token = default)
            where T : notnull
            => NewSequence.FirstOrEmptyAsync(seq, filter, token);

        /// <summary>
        /// Returns the first element in a sequence that satisfies a specified condition.
        /// </summary>
        /// <typeparam name="T">The type of the elements of source.</typeparam>
        /// <param name="seq">A collection to return an element from.</param>
        /// <param name="filter">A function to test each element for a condition.</param>
        /// <param name="token">The token that can be used to cancel enumeration.</param>
        /// <returns>The first element in the sequence that matches to the specified filter; or empty value.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<Optional<T>> FirstOrEmptyAsync<T>(IAsyncEnumerable<T> seq, Predicate<T> filter, CancellationToken token = default)
            where T : notnull
            => NewSequence.FirstOrEmptyAsync(seq, filter, token);

        /// <summary>
        /// Bypasses a specified number of elements in a sequence.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
        /// <param name="enumerator">Enumerator to modify. Cannot be <see langword="null"/>.</param>
        /// <param name="count">The number of elements to skip.</param>
        /// <returns><see langword="true"/>, if current element is available; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<bool> SkipAsync<T>(IAsyncEnumerator<T> enumerator, int count)
            => NewSequence.SkipAsync(enumerator, count);

        /// <summary>
        /// Obtains element at the specified index in the sequence.
        /// </summary>
        /// <typeparam name="T">Type of elements in the sequence.</typeparam>
        /// <param name="collection">Source collection.</param>
        /// <param name="index">Index of the element to read.</param>
        /// <param name="token">The token that can be used to cancel enumeration.</param>
        /// <returns>The requested element; or <see cref="Optional{T}.None"/> if index is out of range.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<Optional<T>> ElementAtAsync<T>(IAsyncEnumerable<T> collection, int index, CancellationToken token = default)
            => NewSequence.ElementAtAsync(collection, index, token);

        /// <summary>
        /// Skip <see langword="null"/> values in the collection.
        /// </summary>
        /// <typeparam name="T">Type of elements in the collection.</typeparam>
        /// <param name="collection">A collection to check. Cannot be <see langword="null"/>.</param>
        /// <returns>Modified lazy collection without <see langword="null"/> values.</returns>
        public static IAsyncEnumerable<T> SkipNulls<T>(IAsyncEnumerable<T?> collection)
            where T : class
            => NewSequence.SkipNulls(collection);

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
        public static Task<T[]> ToArrayAsync<T>(IAsyncEnumerable<T> collection, int initialCapacity = 10, MemoryAllocator<T>? allocator = null, CancellationToken token = default)
            => NewSequence.ToArrayAsync(collection, initialCapacity, allocator, token);
    }
}