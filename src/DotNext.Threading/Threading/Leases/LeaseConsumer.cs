using System.Diagnostics.CodeAnalysis;
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
    private LeaseIdentity identity;

    [SuppressMessage("Usage", "CA2213", Justification = "False positive.")]
    private volatile CancellationTokenSource? source;
    private ITimer timer;
    private Timeout timeout;

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
    public CancellationToken Token => source?.Token ?? new(canceled: true);
    
    /// <summary>
    /// Gets lease expiration timeout.
    /// </summary>
    public ref readonly Timeout Expiration => ref timeout;
    
    /// <summary>
    /// Gets the lease version.
    /// </summary>
    /// <remarks>The returned value can be used as a fencing token.</remarks>
    public LeaseIdentity LeaseId => identity;

    private ValueTask CancelAndStopTimerAsync()
    {
        // Cancel existing source. There is a potential concurrency with LeaseExpired
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

        // ensure that the timer has stopped
        return timer.DisposeAsync();
    }

    /// <summary>
    /// Tries to acquire the lease.
    /// </summary>
    /// <remarks>
    /// This method cancels <see cref="Token"/> immediately. If the method returns <see langword="true"/>, the token
    /// can be used to perform async operation bounded to the lease lifetime.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The expiration timeout; otherwise, <see cref="Timeout.Expired"/>.</returns>
    /// <exception cref="ObjectDisposedException">The consumer is disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async ValueTask<bool> TryAcquireAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        var ts = new Timestamp(provider);
        if (await TryAcquireCoreAsync(token).ConfigureAwait(false) is { } response)
        {
            identity = response.Identity;
            await CancelAndStopTimerAsync().ConfigureAwait(false);

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
        var leaseToken = Token;
        var operationToken = token;
        var cts = operationToken.LinkTo(leaseToken);

        var task = Fork(worker, operationToken);
        try
        {
            Task completedTask;
            do
            {
                completedTask = await Task.WhenAny(Task.Delay(Expiration.Value / 2, operationToken), task).ConfigureAwait(false);
            } while (!ReferenceEquals(completedTask, task) && await TryRenewAsync(token).ConfigureAwait(false));
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
        catch (OperationCanceledException e) when (e.CausedBy(cts, leaseToken))
        {
            throw new TimeoutException(ExceptionMessages.LeaseExpired, e);
        }
        finally
        {
            cts?.Dispose();
        }

        static Task<TResult> Fork(Func<CancellationToken, Task<TResult>> function, CancellationToken token)
            => Task.Run(() => function(token), token);
    }

    /// <summary>
    /// Performs a call to <see cref="LeaseProvider{TMetadata}.TryAcquireAsync(CancellationToken)"/> across the application boundaries.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The response from the lease provider; or <see langword="null"/> if the lease cannot be taken.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected abstract ValueTask<AcquisitionResult?> TryAcquireCoreAsync(CancellationToken token = default);

    /// <summary>
    /// Tries to renew a lease.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// /// <returns>The expiration timeout; otherwise, <see cref="Timeout.Expired"/>.</returns>
    /// <exception cref="ObjectDisposedException">The consumer is disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="InvalidOperationException">This consumer never took the lease.</exception>
    public async ValueTask<bool> TryRenewAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        if (identity.Version is LeaseIdentity.InitialVersion)
            throw new InvalidOperationException();

        var ts = new Timestamp(provider);
        if (await TryRenewCoreAsync(identity, token).ConfigureAwait(false) is { } response)
        {
            identity = response.Identity;

            // ensure that the timer has been stopped
            await timer.DisposeAsync().ConfigureAwait(false);
            timeout = new(AdjustTimeToLive(response.TimeToLive), ts);
            if (timeout.TryGetRemainingTime(out var remainingTime))
            {
                if (source is not { } sourceCopy)
                    source = sourceCopy = new();

                timer = provider.CreateTimer(callback, sourceCopy, remainingTime, InfiniteTimeSpan);
                return true;
            }
        }

        await CancelAndStopTimerAsync().ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Performs a call to <see cref="LeaseProvider{TMetadata}.TryRenewAsync(LeaseIdentity, bool, CancellationToken)"/> or
    /// <see cref="LeaseProvider{TMetadata}.TryAcquireOrRenewAsync(LeaseIdentity, CancellationToken)"/> across the application boundaries.
    /// </summary>
    /// <param name="identity">The identity of the lease to renew.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The response from the lease provider; or <see langword="null"/> if the lease cannot be taken.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected abstract ValueTask<AcquisitionResult?> TryRenewCoreAsync(LeaseIdentity identity, CancellationToken token);

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
    /// <exception cref="InvalidOperationException">This consumer never took the lease.</exception>
    /// <exception cref="InvalidOperationException">This consumer never took the lease.</exception>
    public async ValueTask<bool> ReleaseAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        if (this.identity.Version is LeaseIdentity.InitialVersion)
            throw new InvalidOperationException();

        await CancelAndStopTimerAsync().ConfigureAwait(false);
        timeout = Timeout.Expired;
        if (await ReleaseCoreAsync(this.identity, token).ConfigureAwait(false) is { } identity)
        {
            this.identity = identity;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Performs a call to <see cref="LeaseProvider{TMetadata}.ReleaseAsync(LeaseIdentity, CancellationToken)"/> across
    /// the application boundaries.
    /// </summary>
    /// <param name="identity">The identity of the lease to renew.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The response from the lease provider; or <see langword="null"/> if the lease cannot be taken.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected abstract ValueTask<LeaseIdentity?> ReleaseCoreAsync(LeaseIdentity identity, CancellationToken token);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Interlocked.Exchange(ref source, null)?.Dispose();
            timer.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    protected override ValueTask DisposeAsyncCore()
    {
        Interlocked.Exchange(ref source, null)?.Dispose();
        return timer.DisposeAsync();
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync()"/>
    public new ValueTask DisposeAsync() => base.DisposeAsync();

    /// <summary>
    /// Represents a result of lease acquisition operation.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    protected readonly struct AcquisitionResult
    {
        /// <summary>
        /// Gets or sets the identity of the lease.
        /// </summary>
        public required LeaseIdentity Identity { get; init; }

        /// <summary>
        /// Gets or sets lease expiration time returned by the provider.
        /// </summary>
        public required TimeSpan TimeToLive { get; init; }
    }
}