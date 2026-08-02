using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace DotNext.Threading;

/// <summary>
/// Represents asynchronous broadcast channel for single producer and multiple consumer scenario.
/// </summary>
/// <remarks>
/// The subscription can be created by calling <see cref="IAsyncEnumerable{T}.GetAsyncEnumerator(CancellationToken)"/> method.
/// </remarks>
/// <typeparam name="T">The type of the elements in the sequence.</typeparam>
/// <seealso cref="Collections.Generic.AsyncEnumerable.ForEach{T}(IAsyncEnumerable{T},Func{T,CancellationToken,ValueTask},CancellationToken)"/>
public sealed class AsyncMulticastSequence<T> : IAsyncEnumerable<T>
{
    private ImmutableArray<AsyncListener> listeners = ImmutableArray<AsyncListener>.Empty;

    /// <summary>
    /// Gets a value indicating that this sequence is completed.
    /// </summary>
    public bool IsCompleted
    {
        get
        {
            var copy = listeners;
            Volatile.ReadBarrier();
            return copy.IsDefault;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating how the consumer gets notified about a new value.
    /// </summary>
    /// <value>
    /// <see langword="true"/> means that call to <see cref="IAsyncEnumerator{T}.Current"/> property doesn't
    /// signal the producer about consumption. The confirmation is produced by the next call to
    /// <see cref="IAsyncEnumerator{T}.MoveNextAsync"/> method;
    /// <see langword="false"/> means that call to <see cref="IAsyncEnumerator{T}.Current"/> property
    /// signal the producer about consumption, and the producer can emit a new value without waiting
    /// for the next call to <see cref="IAsyncEnumerator{T}.MoveNextAsync"/> method.
    /// </value>
    public bool NotifyListenersSequentially { get; init; }

    /// <summary>
    /// Sends the value to all attached listeners.
    /// </summary>
    /// <remarks>
    /// This method ensures that all consumers process the supplied value.
    /// </remarks>
    /// <param name="value">The value to produce.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous state of the method.</returns>
    public ValueTask ProduceAsync(T value, CancellationToken token = default)
    {
        var copy = listeners;
        Volatile.ReadBarrier();
        return copy.IsDefaultOrEmpty
            ? ValueTask.CompletedTask
            : BroadcastAsync(ImmutableCollectionsMarshal.AsArray(copy)!, value, token);

        static async ValueTask BroadcastAsync(AsyncListener[] listeners, T value, CancellationToken token)
        {
            foreach (var listener in listeners)
            {
                try
                {
                    await listener.Writer.WriteAsync(value, token).ConfigureAwait(false);
                }
                catch (ChannelClosedException)
                {
                    // ignore exception
                }
            }
        }
    }

    /// <summary>
    /// Tries to complete the sequence and notify all listeners.
    /// </summary>
    /// <param name="e">The exception to broadcast to all listeners.</param>
    /// <returns>
    /// <see langword="true"/> if this sequence is completed successfully;
    /// <see langword="false"/> if this sequence is already completed.
    /// </returns>
    public bool TryComplete(Exception? e = null)
    {
        var result = UnregisterAll(out var detachedListeners);
        if (result)
        {
            Complete(detachedListeners.AsSpan(), e);
        }

        return result;

        static void Complete(ReadOnlySpan<AsyncListener> listeners, Exception? e)
        {
            foreach (var listener in listeners)
            {
                listener.Writer.TryComplete(e);
            }
        }
    }

    private bool UnregisterAll(out ImmutableArray<AsyncListener> detachedListeners)
    {
        detachedListeners = default;
        var reference = new LocalReference<ImmutableArray<AsyncListener>>(ref detachedListeners);
        return ImmutableInterlocked.Update(ref listeners, Erase, reference);
        
        static ImmutableArray<AsyncListener> Erase(ImmutableArray<AsyncListener> listeners, LocalReference<ImmutableArray<AsyncListener>> output)
        {
            output.Value = listeners;
            return default;
        }
    }

    private void Unregister(AsyncListener listener)
    {
        ImmutableInterlocked.Update(ref listeners, Remove, listener);

        static ImmutableArray<AsyncListener> Remove(ImmutableArray<AsyncListener> listeners, AsyncListener listener)
            => listeners.IsDefault ? listeners : listeners.Remove(listener);
    }

    /// <inheritdoc/>
    IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken token)
        => NotifyListenersSequentially
            ? Subscribe(new AsyncListener<SynchronousStrategy>(this, token))
            : Subscribe(new AsyncListener<AsynchronousStrategy>(this, token));

    private IAsyncEnumerator<T> Subscribe<TListener>(TListener listener)
        where TListener : AsyncListener, IAsyncEnumerator<T>
    {
        return ImmutableInterlocked.Update(ref listeners, TrySubscribe, listener)
            ? listener
            : AsyncEnumerable.Empty<T>().GetAsyncEnumerator(listener.Token);

        static ImmutableArray<AsyncListener> TrySubscribe(ImmutableArray<AsyncListener> listeners, TListener listener)
            => listeners.IsDefault ? listeners : listeners.Add(listener);
    }

    private abstract class AsyncListener(AsyncMulticastSequence<T> stream, CancellationToken token)
    {
        private readonly Channel<T> channel = Channel.CreateBounded<T>(capacity: 0);
        private AsyncMulticastSequence<T>? stream = stream;

        public CancellationToken Token => token;

        public ChannelWriter<T> Writer => channel.Writer;

        protected ChannelReader<T> Reader => channel.Reader;

        protected ValueTask<bool> MoveNextAsync()
            => Volatile.Read(in stream) is null ? ValueTask.FromResult(false) : Reader.WaitToReadAsync(token);

        protected void Complete()
        {
            if (stream is not null && Interlocked.Exchange(ref stream, null) is { } detachedStream)
            {
                detachedStream.Unregister(this);
                Writer.TryComplete();
            }
        }
    }
    
    private sealed class AsyncListener<TStrategy>(AsyncMulticastSequence<T> stream, CancellationToken token) : AsyncListener(stream, token), IAsyncEnumerator<T>
        where TStrategy : struct, IConsumerStrategy
    {
        private TStrategy strategy;

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            strategy = default;
            Complete();
            return ValueTask.CompletedTask;
        }

        ValueTask<bool> IAsyncEnumerator<T>.MoveNextAsync()
        {
            strategy.MoveNext(Reader);
            return MoveNextAsync();
        }

        T IAsyncEnumerator<T>.Current => strategy.GetCurrentValue(Reader);
    }
    
    private interface IConsumerStrategy
    {
        T GetCurrentValue(ChannelReader<T> reader);

        void MoveNext(ChannelReader<T> reader);
    }

    [StructLayout(LayoutKind.Auto)]
    private struct SynchronousStrategy : IConsumerStrategy
    {
        private bool hasValue;
        private T? current;

        T IConsumerStrategy.GetCurrentValue(ChannelReader<T> reader)
        {
            if (!hasValue)
            {
                hasValue = reader.TryPeek(out current);
            }

            return current!;
        }

        void IConsumerStrategy.MoveNext(ChannelReader<T> reader)
        {
            // skip the value
            if (hasValue)
            {
                reader.TryRead(out _);
                hasValue = false;
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private struct AsynchronousStrategy : IConsumerStrategy
    {
        private bool hasValue;
        private T? current;

        T IConsumerStrategy.GetCurrentValue(ChannelReader<T> reader)
        {
            if (!hasValue)
            {
                hasValue = reader.TryRead(out current);
            }

            return current!;
        }

        void IConsumerStrategy.MoveNext(ChannelReader<T> reader) => hasValue = false;
    }
}