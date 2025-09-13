using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Threading;

using Tasks;
using Tasks.Pooling;

/// <summary>
/// Represents a synchronization point at which two async flows can cooperate and swap elements
/// within pairs.
/// </summary>
/// <remarks>
/// This type is useful to organize pipeline between consumer and producer.
/// </remarks>
/// <typeparam name="T">The type of objects that may be exchanged.</typeparam>
[DebuggerDisplay($"CanBeCompletedSynchronously = {{{nameof(CanBeCompletedSynchronously)}}}, Terminated = {{{nameof(IsTerminated)}}}")]
public class AsyncExchanger<T> : Disposable, IAsyncDisposable
{
    private sealed class ExchangePoint : LinkedValueTaskCompletionSource<T>
    {
        private AsyncExchanger<T>? owner;
        private T? value;

        internal void Initialize(AsyncExchanger<T> owner, T value)
        {
            this.owner = owner;
            this.value = value;
        }

        protected override void AfterConsumed() => owner?.OnCompleted(this);

        protected override void CleanUp()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                value = default;

            owner = null;
            base.CleanUp();
        }

        internal bool TryExchange(ref T value, out bool resumable)
        {
            if (TrySetResult(completionData: null, completionToken: null, value, out resumable))
            {
                value = this.value!;
                return true;
            }

            return false;
        }
    }

    private readonly TaskCompletionSource disposeTask;
    private ValueTaskPool<T> pool;
    private ExchangePoint? point;
    private volatile ExchangeTerminatedException? termination;

    /// <summary>
    /// Initializes a new asynchronous exchanger.
    /// </summary>
    public AsyncExchanger()
    {
        pool = new();
        disposeTask = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private object SyncRoot => disposeTask;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    [ExcludeFromCodeCoverage]
    private bool CanBeCompletedSynchronously => point is { IsCompleted: false };

    private ExchangePoint RentExchangePoint(T value)
    {
        Debug.Assert(Monitor.IsEntered(SyncRoot));

        var result = pool.Rent<ExchangePoint>();
        result.Initialize(this, value);
        return result;
    }

    private void OnCompleted(ExchangePoint point)
    {
        Monitor.Enter(SyncRoot);
        if (ReferenceEquals(this.point, point))
        {
            this.point = null;
        }

        Monitor.Exit(SyncRoot);

        if (point.TryReset(out _))
        {
            pool.Return(point);
        }
    }

    /// <summary>
    /// Waits for another flow to arrive at this exchange point,
    /// then transfers the given object to it, receiving its object as return value.
    /// </summary>
    /// <param name="value">The object to exchange.</param>
    /// <param name="timeout">The maximum time to wait.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The object provided by another async flow.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative.</exception>
    /// <exception cref="TimeoutException">The operation has timed out.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled or timed out.</exception>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="ExchangeTerminatedException">The exchange has been terminated.</exception>
    public ValueTask<T> ExchangeAsync(T value, TimeSpan timeout, CancellationToken token = default)
    {
        ValueTask<T> result;

        switch (timeout.Ticks)
        {
            case Timeout.InfiniteTicks:
                goto default;
            case < 0L or > Timeout.MaxTimeoutParameterTicks:
                result = ValueTask.FromException<T>(new ArgumentOutOfRangeException(nameof(timeout)));
                break;
            case 0L:
                try
                {
                    result = TryExchange(ref value)
                        ? new(value)
                        : ValueTask.FromException<T>(new TimeoutException());
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException<T>(e);
                }

                break;
            default:
                ManualResetCompletionSource? suspendedCaller;
                ISupplier<TimeSpan, CancellationToken, ValueTask<T>> factory;
                lock (SyncRoot)
                {
                    if (IsDisposed)
                    {
                        result = new(GetDisposedTask<T>());
                    }
                    else if (IsDisposing)
                    {
                        Dispose(true);
                        result = new(GetDisposedTask<T>());
                    }
                    else if (termination is not null)
                    {
                        result = ValueTask.FromException<T>(termination);
                    }
                    else if (point?.TryExchange(ref value, out var resumable) ?? false)
                    {
                        suspendedCaller = resumable ? point : null;
                        point = null;
                        result = new(value);
                        goto resume_callers;
                    }
                    else
                    {
                        factory = point = RentExchangePoint(value);
                        goto create_task;
                    }
                }

                break;

            resume_callers:
                suspendedCaller?.Resume();
                break;

            create_task:
                result = factory.Invoke(timeout, token);
                break;
        }

        return result;
    }

    /// <summary>
    /// Waits for another flow to arrive at this exchange point,
    /// then transfers the given object to it, receiving its object as return value.
    /// </summary>
    /// <param name="value">The object to exchange.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The object provided by another async flow.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="ExchangeTerminatedException">The exchange has been terminated.</exception>
    public ValueTask<T> ExchangeAsync(T value, CancellationToken token = default)
        => ExchangeAsync(value, new(Timeout.InfiniteTicks), token);

    /// <summary>
    /// Attempts to transfer the object to another flow synchronously.
    /// </summary>
    /// <remarks>
    /// <paramref name="value"/> remains unchanged if return value is <see langword="false"/>.
    /// </remarks>
    /// <param name="value">The object to exchange.</param>
    /// <returns><see langword="true"/> if another flow is ready for exchange; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="ExchangeTerminatedException">The exchange has been terminated.</exception>
    public bool TryExchange(ref T value)
    {
        bool result;
        var suspendedCaller = default(ManualResetCompletionSource);

        lock (SyncRoot)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);

            if (IsDisposing)
            {
                Dispose(true);
                result = false;
            }
            else if (termination is not null)
            {
                throw termination;
            }
            else if (point is null)
            {
                result = false;
            }
            else
            {
                result = point.TryExchange(ref value, out var resumable);
                suspendedCaller = resumable ? point : null;
                point = null;
            }
        }

        suspendedCaller?.Resume();
        return result;
    }

    /// <summary>
    /// Informs another participant that no more data will be exchanged with it.
    /// </summary>
    /// <param name="exception">The optional exception indicating termination reason.</param>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The exchange is already terminated.</exception>
    public void Terminate(Exception? exception = null)
    {
        lock (SyncRoot)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            if (termination is not null)
                throw new InvalidOperationException();

            ExchangeTerminatedException tmp;
            termination = tmp = new ExchangeTerminatedException(exception);
            if (point?.TrySetException(tmp) ?? false)
                point = null;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this exchange has been terminated.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    public bool IsTerminated
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return termination is not null;
        }
    }

    private void NotifyObjectDisposed()
    {
        lock (SyncRoot)
        {
            point?.TrySetException(CreateException());
            point = null;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            NotifyObjectDisposed();
            termination = null;
            disposeTask.TrySetResult();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore()
    {
        ValueTask result;

        lock (SyncRoot)
        {
            if (point is null or { IsCompleted: true })
            {
                Dispose(true);
                result = ValueTask.CompletedTask;
            }
            else
            {
                result = new(disposeTask.Task);
            }
        }

        return result;
    }

    /// <summary>
    /// Provides graceful shutdown of this instance.
    /// </summary>
    /// <returns>The task representing state of asynchronous graceful shutdown.</returns>
    public new ValueTask DisposeAsync() => base.DisposeAsync();
}