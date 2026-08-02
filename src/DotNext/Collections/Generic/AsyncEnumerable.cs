using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace DotNext.Collections.Generic;

using Runtime.CompilerServices;

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
        /// Starts background task that listens for the items returned by the enumerator.
        /// </summary>
        /// <param name="acceptor">
        /// The callback for the elements returned by the enumerator. If it throws,
        /// the underlying enumerator disposes and the listener is not able to accept new items.
        /// </param>
        /// <param name="token">The token that can be used to cancel the listener.</param>
        /// <returns>The object that control listener lifetime. Call to <see cref="IAsyncDisposable.DisposeAsync"/>
        /// stops the listener from receiving items from the enumerator.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="acceptor"/> is <see langword="null"/>.</exception>
        public IAsyncDisposable ForEach(Func<T, CancellationToken, ValueTask> acceptor, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(acceptor);

            return new SequenceListener<T>(collection, acceptor, token);
        }

        /// <summary>
        /// Starts background task that listens for the items returned by the enumerator.
        /// </summary>
        /// <param name="acceptor">
        /// The callback for the elements returned by the enumerator. If it throws,
        /// the underlying enumerator disposes and the listener is not able to accept new items.
        /// </param>
        /// <param name="token">The token that can be used to cancel the listener.</param>
        /// <param name="completion">A task that turns into completed state when the underlying enumerator
        /// has no more elements; or when <see cref="IAsyncDisposable.DisposeAsync()"/> is called on the returned object.</param>
        /// <returns>The object that control listener lifetime. Call to <see cref="IAsyncDisposable.DisposeAsync"/>
        /// stops the listener from receiving items from the enumerator.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="acceptor"/> is <see langword="null"/>.</exception>
        public IAsyncDisposable ForEach(Func<T, CancellationToken, ValueTask> acceptor, out Task completion, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(acceptor);

            var listener = new SequenceListener<T>(collection, acceptor, token);
            completion = listener.Completion;
            return listener;
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
        
        /// <summary>
        /// Constructs read-only sequence with a single item in it.
        /// </summary>
        /// <param name="item">An item to be placed into list.</param>
        /// <returns>Read-only list containing single item.</returns>
        public static IAsyncEnumerable<T> Singleton(T item)
            => new Specialized.SingletonList<T> { Item = item };
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
}

file sealed class ThrowingEnumerator<T>(Exception exception) : IAsyncEnumerator<T>, IAsyncEnumerable<T>
    where T : allows ref struct
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public T Current
    {
        get
        {
            ExceptionDispatchInfo.Throw(exception);
            return default;
        }
    }

    ValueTask<bool> IAsyncEnumerator<T>.MoveNextAsync() => ValueTask.FromException<bool>(exception);

    IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken cancellationToken) => this;

    ValueTask IAsyncDisposable.DisposeAsync() => ValueTask.CompletedTask;
}

file sealed class SequenceListener<T> : IAsyncDisposable
    where T : allows ref struct
{
    private readonly Func<T, CancellationToken, ValueTask> listener;
    private readonly IAsyncEnumerator<T> enumerator;

    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private CancellationTokenSource? listenerTokenSource;

    public SequenceListener(IAsyncEnumerable<T> sequence, Func<T, CancellationToken, ValueTask> listener, CancellationToken token)
    {
        Debug.Assert(sequence is not null);
        Debug.Assert(listener is not null);

        token = (listenerTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token)).Token;
        enumerator = sequence.GetAsyncEnumerator(token);
        this.listener = listener;
        Completion = ListenAsync(token);
    }

    public Task Completion { get; }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task ListenAsync(CancellationToken token)
    {
        try
        {
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                await listener(enumerator.Current, token).ConfigureAwait(false);
            }
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask DisposeAsync(CancellationTokenSource cts)
    {
        using (cts)
        {
            await cts.CancelAsync().ConfigureAwait(false);
            await Completion.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync() => listenerTokenSource is not null && Interlocked.Exchange(ref listenerTokenSource, null) is { } cts
        ? DisposeAsync(cts)
        : ValueTask.CompletedTask;
}