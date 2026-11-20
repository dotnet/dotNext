using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Threading.Timeout;

namespace DotNext.Threading.Leases;

using Diagnostics;

/// <summary>
/// Represents client side of a lease in a distributed environment.
/// </summary>
/// <seealso cref="LeaseProvider{TMetadata}"/>
public abstract class LeaseConsumer : Disposable, IAsyncDisposable
{
    private readonly double clockDriftBound;
    private readonly TimeProvider provider;
    private readonly TimerCallback callback;
    private long version;

    [SuppressMessage("Usage", "CA2213", Justification = "False positive.")]
    private volatile CancellationTokenSource? source;
    private ITimer timer;
    private Timeout timeout;
    private bool acquired;

    /// <summary>
    /// Initializes a new lease consumer.
    /// </summary>
    /// <param name="provider">The timer factory.</param>
    protected LeaseConsumer(TimeProvider? provider = null)
    {
        clockDriftBound = 1D;
        this.provider = provider ?? TimeProvider.System;
        callback = LeaseExpired;
        timer = this.provider.CreateTimer(callback, state: null, InfiniteTimeSpan, InfiniteTimeSpan);
        timeout = Timeout.Expired;
    }

    private void LeaseExpired(CancellationTokenSource cts)
    {
        if (ReferenceEquals(Interlocked.CompareExchange(ref source, null, cts), cts))
        {
            try
            {
                cts.Cancel(throwOnFirstException: false);
            }
            finally
            {
                cts.Dispose();
            }
        }
    }

    private void LeaseExpired(object? state)
    {
        if (state is CancellationTokenSource cts)
            LeaseExpired(cts);
    }

    /// <summary>
    /// Gets or sets wall clock desync degree in the cluster.
    /// </summary>
    /// <value>A value in range [1..∞). The default value is 1.</value>
    public double ClockDriftBound
    {
        get => clockDriftBound;
        init => clockDriftBound = double.IsFinite(value) && value >= 1D ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    private TimeSpan AdjustTimeToLive(TimeSpan originalTtl)
        => originalTtl / clockDriftBound;

    /// <summary>
    /// Gets the token bounded to the lease lifetime.
    /// </summary>
    public CancellationToken Token
    {
        get
        {
            if (source is { } sourceCopy)
            {
                // skip the disposal check: the source is always canceled before disposal
                if (CancellationToken.CanBeInlined)
                    return Unsafe.BitCast<ValueTuple<CancellationTokenSource>, CancellationToken>(new(sourceCopy));

                try
                {
                    return sourceCopy.Token;
                }
                catch (ObjectDisposedException)
                {
                    // the source is canceled before disposal, concurrently with the current call
                }
            }

            return new(canceled: true);
        }
    }
    
    /// <summary>
    /// Gets lease expiration timeout.
    /// </summary>
    public ref readonly Timeout Expiration => ref timeout;
    
    /// <summary>
    /// Gets the lease version.
    /// </summary>
    /// <remarks>
    /// The returned value can be used as a fencing token. The property can be read concurrently with the renewal of the lease.
    /// </remarks>
    /// <value>The version of the lease. The value is meaningful only after the lease has been acquired.</value>
    public long Version => Volatile.Read(in version);

    private void Terminate()
    {
        // There is a potential concurrency with LeaseExpired
        if (Interlocked.Exchange(ref source, null) is { } sourceCopy)
        {
            try
            {
                sourceCopy.Cancel(throwOnFirstException: false);
            }
            finally
            {
                sourceCopy.Dispose();
            }
        }
    }

    private ValueTask TerminateAsync()
    {
        Terminate();

        // ensure that the timer has stopped
        return timer.DisposeAsync();
    }

    /// <summary>
    /// Tries to acquire the lease.
    /// </summary>
    /// <remarks>
    /// If the lease is acquired, the previously obtained <see cref="Token"/> is canceled and the new token is issued.
    /// It can be used to perform async operation bounded to the lease lifetime. If the provider rejects
    /// the request, the state of the currently held lease remains unchanged.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the lease is acquired successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The consumer is disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async ValueTask<bool> TryAcquireAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        var ts = new Timestamp(provider);
        if (await TryAcquireCoreAsync(token).ConfigureAwait(false) is { } response)
        {
            acquired = true;
            Volatile.Write(ref version, response.Version);
            await TerminateAsync().ConfigureAwait(false);

            timeout = new(AdjustTimeToLive(response.TimeToLive), ts);
            if (timeout.TryGetRemainingTime(provider, out var remainingTime))
            {
                timer = provider.CreateTimer(callback, source = new(), remainingTime, InfiniteTimeSpan);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Acquires the lease.
    /// </summary>
    /// <param name="pauseDuration">The time to wait between <see cref="TryAcquireAsync(CancellationToken)"/> calls.</param>
    /// <param name="pauseRandomizer">
    /// The source of random values that can be used to generate random pauses between <see cref="TryAcquireAsync(CancellationToken)"/> calls.
    /// If <see langword="null"/> then use a value of <paramref name="pauseDuration"/>.
    /// </param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution of this method.</returns>
    /// <exception cref="ObjectDisposedException">The consumer is disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pauseDuration"/> is less than or equal to <see cref="TimeSpan.Zero"/>; or greater than <see cref="Timeout.MaxTimeoutParameterTicks"/>.</exception>
    public async ValueTask AcquireAsync(TimeSpan pauseDuration, Random? pauseRandomizer = null, CancellationToken token = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pauseDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pauseDuration.Ticks, Timeout.MaxTimeoutParameterTicks, nameof(pauseDuration));

        while (!await TryAcquireAsync(token).ConfigureAwait(false))
        {
            var delay = pauseRandomizer is null
                ? pauseDuration
                : new(pauseRandomizer.NextInt64(pauseDuration.Ticks) + 1L);

            await Task.Delay(delay, provider, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Executes the specified long-running operation that is protected by the lease lifetime.
    /// </summary>
    /// <remarks>
    /// During execution, the lease is renewed automatically. On return, this method guarantees that the execution
    /// of <paramref name="worker"/> is completed.
    /// </remarks>
    /// <param name="worker">The function to be executed in the background.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <returns>The value returned by <paramref name="worker"/>.</returns>
    /// <exception cref="ObjectDisposedException">The consumer is disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="TimeoutException">The lease has been expired.</exception>
    /// <exception cref="AggregateException"><see cref="TryRenewAsync"/> throws an exception, and it is combined with the exception from <paramref name="worker"/>.</exception>
    public async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> worker, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var exception = default(Exception?);
        var task = Fork(worker, out var cts, out var leaseToken, token);
        var timerToken = task.AsCancellationToken();
        try
        {
            do
            {
                var delay = Expiration.TryGetRemainingTime(provider, out var remainingTime)
                    ? remainingTime / 2
                    : TimeSpan.Zero;

                await Task.Delay(delay, provider, timerToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            } while (!task.IsCompleted && await TryRenewAsync(token).ConfigureAwait(false));
        }
        catch (Exception e)
        {
            exception = e;
        }
        finally
        {
            cts?.Cancel(throwOnFirstException: false);
        }

        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (Exception e) when (exception is not null)
        {
            throw new AggregateException(exception, e);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == leaseToken ||
                                                   e.CancellationToken == cts?.Token && cts.CancellationOrigin == leaseToken)
        {
            throw new TimeoutException(ExceptionMessages.LeaseExpired, e);
        }
        catch (OperationCanceledException e) when (cts is not null)
        {
            throw new OperationCanceledException(e.Message, e, cts.CancellationOrigin);
        }
        finally
        {
            cts?.Dispose();
        }
    }

    private Task<TResult> Fork<TResult>(Func<CancellationToken, Task<TResult>> worker, out LinkedCancellationTokenSource? cts,
        out CancellationToken leaseToken,
        CancellationToken token)
    {
        cts = CancellationToken.Combine(ref token, leaseToken = Token);
        return token.IsCancellationRequested ? Task.FromCanceled<TResult>(token) : Task.Run(worker, token);
    }

    /// <summary>
    /// Performs a call to <see cref="LeaseProvider{TMetadata}.TryAcquireAsync(Guid, CancellationToken)"/> across the application boundaries.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The response from the lease provider; or <see langword="null"/> if the lease cannot be taken.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected abstract ValueTask<AcquisitionResult?> TryAcquireCoreAsync(CancellationToken token = default);

    /// <summary>
    /// Tries to renew a lease.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the lease is renewed successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The consumer is disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="InvalidOperationException">This consumer never took the lease, or it has been released.</exception>
    public async ValueTask<bool> TryRenewAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        if (!acquired)
            throw new InvalidOperationException();

        var ts = new Timestamp(provider);
        if (await TryRenewCoreAsync(token).ConfigureAwait(false) is { } response)
        {
            Volatile.Write(ref version, response.Version);

            // ensure that the timer has been stopped
            await timer.DisposeAsync().ConfigureAwait(false);
            timeout = new(AdjustTimeToLive(response.TimeToLive), ts);
            if (timeout.TryGetRemainingTime(provider, out var remainingTime))
            {
                if (source is not { } sourceCopy)
                    source = sourceCopy = new();

                timer = provider.CreateTimer(callback, sourceCopy, remainingTime, InfiniteTimeSpan);
                return true;
            }
        }

        await TerminateAsync().ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Performs a call to <see cref="LeaseProvider{TMetadata}.TryRenewAsync(Guid, bool, CancellationToken)"/> across the application boundaries.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The response from the lease provider; or <see langword="null"/> if the lease cannot be taken.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected abstract ValueTask<AcquisitionResult?> TryRenewCoreAsync(CancellationToken token);

    /// <summary>
    /// Releases a lease.
    /// </summary>
    /// <remarks>
    /// This method cancels <see cref="Token"/> immediately.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if lease canceled successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ObjectDisposedException">The consumer is disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="InvalidOperationException">This consumer never took the lease, or it has been released.</exception>
    public async ValueTask<bool> ReleaseAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        if (!acquired)
            throw new InvalidOperationException();

        await TerminateAsync().ConfigureAwait(false);
        timeout = Timeout.Expired;
        if (await ReleaseCoreAsync(token).ConfigureAwait(false) is { } releasedVersion)
        {
            Volatile.Write(ref version, releasedVersion);
            acquired = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Performs a call to <see cref="LeaseProvider{TMetadata}.ReleaseAsync(Guid, CancellationToken)"/> across
    /// the application boundaries.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The response from the lease provider; or <see langword="null"/> if the lease cannot be taken.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected abstract ValueTask<long?> ReleaseCoreAsync(CancellationToken token);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Terminate();
            timer.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    protected override ValueTask DisposeAsyncCore() => TerminateAsync();

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync()"/>
    public new ValueTask DisposeAsync() => base.DisposeAsync();

    /// <summary>
    /// Represents a result of lease acquisition operation.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    protected readonly struct AcquisitionResult
    {
        /// <summary>
        /// Gets or sets the version of the lease.
        /// </summary>
        public required long Version { get; init; }

        /// <summary>
        /// Gets or sets lease expiration time returned by the provider.
        /// </summary>
        public required TimeSpan TimeToLive { get; init; }
    }
}