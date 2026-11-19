using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Leases;

using Diagnostics;

/// <summary>
/// Represents provider side of a lease in a distributed environment.
/// </summary>
/// <remarks>
/// An instance of this type must support concurrent calls.
/// <para>
/// Each instance tracks the lease expiration using its own monotonic clock, starting from the moment it observes
/// a particular version of the lease. Thus, the wall clocks of the instances sharing the same storage don't need
/// to be synchronized. As a consequence, a newly created instance waits for the full <see cref="TimeToLive"/>
/// before taking over the lease that was not released explicitly. Call <see cref="ObserveAsync(CancellationToken)"/>
/// immediately after the instantiation to start tracking the expiration as early as possible.
/// </para>
/// <para>
/// The lease is held on behalf of the owner identified by <see cref="Guid"/>. The owner must be unique
/// across all consumers of the lease. Acquisition, renewal and release by the owner don't require the actual
/// version of the lease. Thus, these operations can be retried safely if the response is lost.
/// </para>
/// </remarks>
/// <typeparam name="TMetadata">The type of metadata associated with a lease.</typeparam>
/// <seealso cref="LeaseConsumer"/>
public abstract partial class LeaseProvider<TMetadata> : Disposable
{
    private readonly TimeProvider provider;
    private readonly CancellationTokenMultiplexer cancellationTokens;

    [SuppressMessage("Usage", "CA2213", Justification = "Disposed using Terminate() method")]
    private volatile CancellationTokenSource? lifetimeTokenSource;

    // the last observed version of the lease and the local time of its first observation
    private Atomic<Observation> observation;

    /// <summary>
    /// Initializes a new instance of lease provider.
    /// </summary>
    /// <param name="ttl">The lease expiration timeout.</param>
    /// <param name="provider">The time provider.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ttl"/> is not positive or greater than <see cref="Timeout.MaxTimeoutParameterTicks"/>.</exception>
    protected LeaseProvider(TimeSpan ttl, TimeProvider? provider = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(ttl.Ticks, Timeout.MaxTimeoutParameterTicks, nameof(ttl));

        this.provider = provider ?? TimeProvider.System;
        TimeToLive = ttl;
        lifetimeTokenSource = new();
        LifetimeToken = lifetimeTokenSource.Token;
        cancellationTokens = new() { MaximumRetained = 100 };
    }

    /// <summary>
    /// A token that represents state of this object.
    /// </summary>
    /// <remarks>
    /// A call to <see cref="Dispose(bool)"/> cancels the token.
    /// </remarks>
    protected CancellationToken LifetimeToken { get; } // cached to avoid ObjectDisposedException

    /// <summary>
    /// Gets a lease time-to-live.
    /// </summary>
    public TimeSpan TimeToLive { get; }

    private async ValueTask<AcquisitionResult?> TryChangeStateAsync<TCondition, TUpdater>(TCondition condition, TUpdater updater, Guid owner, CancellationToken token)
        where TCondition : ITransitionCondition
        where TUpdater : ISupplier<TMetadata, CancellationToken, ValueTask<TMetadata>>
    {
        var cts = cancellationTokens.Combine(token, LifetimeToken);
        try
        {
            var state = await GetStateAsync(cts.Token).ConfigureAwait(false);

            if (!condition.Invoke(in state, GetRemainingTime(in state) <= TimeSpan.Zero))
                return null;

            var metadata = await updater.Invoke(state.Metadata, cts.Token).ConfigureAwait(false);

            // the timestamp must precede CreatedAt, so the reported remaining time never exceeds the stored one
            var ts = new Timestamp(provider);
            state = new()
            {
                CreatedAt = provider.GetUtcNow(),
                Identity = state.Identity.BumpVersion(),
                Metadata = metadata,
                OwnerId = owner,
            };

            if (!await TryUpdateStateAsync(state, cts.Token).ConfigureAwait(false))
                return null;

            observation.Value = new(state.Identity, ts);
            var remainingTime = TimeToLive - ts.GetElapsedTime(provider);
            return remainingTime > TimeSpan.Zero
                ? new AcquisitionResult(in state, remainingTime, provider)
                : null;
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, LifetimeToken))
        {
            throw CreateException();
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts.Token)
        {
            throw new OperationCanceledException(e.Message, e, cts.CancellationOrigin);
        }
        finally
        {
            await cts.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Tries to acquire the lease.
    /// </summary>
    /// <remarks>
    /// If the lease is already held by <paramref name="owner"/>, it is renewed.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="owner">The unique identifier of the lease owner.</param>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The acquisition result; or <see langword="null"/> if the lease is taken by another owner.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<AcquisitionResult?> TryAcquireAsync<TArg>(Guid owner, TArg arg, Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token = default)
        => CheckArguments(owner, updater) is { } e
            ? ValueTask.FromException<AcquisitionResult?>(e)
            : TryChangeStateAsync(new AcquisitionCondition(owner), new Updater<TArg>(arg, updater), owner, token);

    /// <summary>
    /// Tries to acquire the lease.
    /// </summary>
    /// <remarks>
    /// If the lease is already held by <paramref name="owner"/>, it is renewed.
    /// </remarks>
    /// <param name="owner">The unique identifier of the lease owner.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The acquisition result; or <see langword="null"/> if the lease is taken by another owner.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<AcquisitionResult?> TryAcquireAsync(Guid owner, CancellationToken token = default)
        => CheckArguments(owner) is { } e
            ? ValueTask.FromException<AcquisitionResult?>(e)
            : TryChangeStateAsync(new AcquisitionCondition(owner), NoOpUpdater.Instance, owner, token);

    private async ValueTask<AcquisitionResult> AcquireAsync<TUpdater>(Guid owner, TUpdater updater, CancellationToken token)
        where TUpdater : ISupplier<TMetadata, CancellationToken, ValueTask<TMetadata>>
    {
        var cts = cancellationTokens.Combine(token, LifetimeToken);
        try
        {
            while (true)
            {
                var state = await GetStateAsync(cts.Token).ConfigureAwait(false);

                var remainingTime = GetRemainingTime(in state);
                if (remainingTime <= TimeSpan.Zero || state.IsOwnedBy(owner))
                {
                    var metadata = await updater.Invoke(state.Metadata, cts.Token).ConfigureAwait(false);

                    // the timestamp must precede CreatedAt, so the reported remaining time never exceeds the stored one
                    var ts = new Timestamp(provider);
                    state = new()
                    {
                        CreatedAt = provider.GetUtcNow(),
                        Identity = state.Identity.BumpVersion(),
                        Metadata = metadata,
                        OwnerId = owner,
                    };

                    if (!await TryUpdateStateAsync(state, cts.Token).ConfigureAwait(false))
                        continue;

                    observation.Value = new(state.Identity, ts);
                    remainingTime = TimeToLive - ts.GetElapsedTime(provider);

                    if (remainingTime <= TimeSpan.Zero)
                        continue;

                    return new AcquisitionResult(in state, remainingTime, provider);
                }

                await Task.Delay(remainingTime, provider, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, LifetimeToken))
        {
            throw CreateException();
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts.Token)
        {
            throw new OperationCanceledException(e.Message, e, cts.CancellationOrigin);
        }
        finally
        {
            await cts.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Acquires the lease.
    /// </summary>
    /// <remarks>
    /// If the lease is already held by <paramref name="owner"/>, it is renewed.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="owner">The unique identifier of the lease owner.</param>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<AcquisitionResult> AcquireAsync<TArg>(Guid owner, TArg arg, Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token = default)
        => CheckArguments(owner, updater) is { } e
            ? ValueTask.FromException<AcquisitionResult>(e)
            : AcquireAsync(owner, new Updater<TArg>(arg, updater), token);

    /// <summary>
    /// Acquires the lease.
    /// </summary>
    /// <remarks>
    /// If the lease is already held by <paramref name="owner"/>, it is renewed.
    /// </remarks>
    /// <param name="owner">The unique identifier of the lease owner.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<AcquisitionResult> AcquireAsync(Guid owner, CancellationToken token = default)
        => CheckArguments(owner) is { } e
            ? ValueTask.FromException<AcquisitionResult>(e)
            : AcquireAsync(owner, NoOpUpdater.Instance, token);

    /// <summary>
    /// Tries to renew the lease.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="owner">The unique identifier of the lease owner.</param>
    /// <param name="reacquire"><see langword="true"/> to acquire the lease on renewal if it is expired.</param>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The status of the operation; or <see langword="null"/> if the lease is taken by another owner, released, or expired.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<AcquisitionResult?> TryRenewAsync<TArg>(Guid owner, bool reacquire, TArg arg, Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token = default)
        => CheckArguments(owner, updater) is { } e
            ? ValueTask.FromException<AcquisitionResult?>(e)
            : TryChangeStateAsync(new RenewalCondition(owner, reacquire), new Updater<TArg>(arg, updater), owner, token);

    /// <summary>
    /// Tries to renew the lease.
    /// </summary>
    /// <param name="owner">The unique identifier of the lease owner.</param>
    /// <param name="reacquire"><see langword="true"/> to acquire the lease on renewal if it is expired.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The status of the operation; or <see langword="null"/> if the lease is taken by another owner, released, or expired.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<AcquisitionResult?> TryRenewAsync(Guid owner, bool reacquire, CancellationToken token = default)
        => CheckArguments(owner) is { } e
            ? ValueTask.FromException<AcquisitionResult?>(e)
            : TryChangeStateAsync(new RenewalCondition(owner, reacquire), NoOpUpdater.Instance, owner, token);

    private async ValueTask<LeaseIdentity?> ReleaseAsync<TUpdater>(Guid owner, TUpdater updater, CancellationToken token)
        where TUpdater : ISupplier<TMetadata, CancellationToken, ValueTask<TMetadata>>
    {
        var cts = cancellationTokens.Combine(token, LifetimeToken);
        try
        {
            var state = await GetStateAsync(cts.Token).ConfigureAwait(false);

            // the expired lease can be released, because it's not taken by another owner
            if (!state.IsOwnedBy(owner))
                return null;

            state = new()
            {
                CreatedAt = default,
                Identity = state.Identity.BumpVersion(),
                Metadata = await updater.Invoke(state.Metadata, cts.Token).ConfigureAwait(false),
                OwnerId = Guid.Empty,
            };

            return await TryUpdateStateAsync(state, cts.Token).ConfigureAwait(false) ? state.Identity : null;
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, LifetimeToken))
        {
            throw CreateException();
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts.Token)
        {
            throw new OperationCanceledException(e.Message, e, cts.CancellationOrigin);
        }
        finally
        {
            await cts.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Releases the lease.
    /// </summary>
    /// <remarks>
    /// The expired lease can be released as well if it is not taken by another owner.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="owner">The unique identifier of the lease owner.</param>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Updated lease identity; or <see langword="null"/> if the lease is taken by another owner or already released.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<LeaseIdentity?> ReleaseAsync<TArg>(Guid owner, TArg arg, Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token = default)
        => CheckArguments(owner, updater) is { } e
            ? ValueTask.FromException<LeaseIdentity?>(e)
            : ReleaseAsync(owner, new Updater<TArg>(arg, updater), token);

    /// <summary>
    /// Releases the lease.
    /// </summary>
    /// <remarks>
    /// The expired lease can be released as well if it is not taken by another owner.
    /// </remarks>
    /// <param name="owner">The unique identifier of the lease owner.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Updated lease identity; or <see langword="null"/> if the lease is taken by another owner or already released.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentException"><paramref name="owner"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<LeaseIdentity?> ReleaseAsync(Guid owner, CancellationToken token = default)
        => CheckArguments(owner) is { } e
            ? ValueTask.FromException<LeaseIdentity?>(e)
            : ReleaseAsync(owner, NoOpUpdater.Instance, token);

    private static ArgumentException? CheckArguments(Guid owner)
        => owner == Guid.Empty ? new(ExceptionMessages.EmptyLeaseOwner, nameof(owner)) : null;

    private static ArgumentException? CheckArguments(Guid owner, Delegate? updater)
        => CheckArguments(owner) ?? (updater is null ? new ArgumentNullException(nameof(updater)) : null);

    private async ValueTask<LeaseIdentity?> UnsafeTryReleaseAsync<TUpdater>(TUpdater updater, CancellationToken token)
        where TUpdater : ISupplier<TMetadata, CancellationToken, ValueTask<TMetadata>>
    {
        var cts = cancellationTokens.Combine(token, LifetimeToken);
        try
        {
            var state = await GetStateAsync(cts.Token).ConfigureAwait(false);

            state = new()
            {
                CreatedAt = default,
                Identity = state.Identity.BumpVersion(),
                Metadata = await updater.Invoke(state.Metadata, cts.Token).ConfigureAwait(false),
                OwnerId = Guid.Empty,
            };

            return await TryUpdateStateAsync(state, cts.Token).ConfigureAwait(false) ? state.Identity : null;
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts,LifetimeToken))
        {
            throw CreateException();
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts.Token)
        {
            throw new OperationCanceledException(e.Message, e, cts.CancellationOrigin);
        }
        finally
        {
            await cts.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Tries to release the lease ungracefully.
    /// </summary>
    /// <remarks>
    /// It's possible to call <see cref="AcquireAsync(Guid, CancellationToken)"/> method after the current one
    /// and get a new lease while the existing owner thinks that the lease is exclusively owned.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Updated lease identity; or <see langword="null"/> if updated or taken by another process.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
    public ValueTask<LeaseIdentity?> UnsafeTryReleaseAsync<TArg>(TArg arg, Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token = default)
        => updater is not null ? UnsafeTryReleaseAsync(new Updater<TArg>(arg, updater), token) : ValueTask.FromException<LeaseIdentity?>(new ArgumentNullException(nameof(updater)));

    /// <summary>
    /// Tries to release the lease ungracefully.
    /// </summary>
    /// <remarks>
    /// It's possible to call <see cref="AcquireAsync(Guid, CancellationToken)"/> method after the current one
    /// and get a new lease while the existing owner thinks that the lease is exclusively owned.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Updated lease identity; or <see langword="null"/> if updated or taken by another process.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<LeaseIdentity?> UnsafeTryReleaseAsync(CancellationToken token = default)
        => UnsafeTryReleaseAsync(NoOpUpdater.Instance, token);

    private async ValueTask<LeaseIdentity> UnsafeReleaseAsync<TUpdater>(TUpdater updater, CancellationToken token)
        where TUpdater : ISupplier<TMetadata, CancellationToken, ValueTask<TMetadata>>
    {
        var cts = cancellationTokens.Combine(token, LifetimeToken);
        try
        {
            State state;
            do
            {
                state = await GetStateAsync(cts.Token).ConfigureAwait(false);

                state = new()
                {
                    CreatedAt = default,
                    Identity = state.Identity.BumpVersion(),
                    Metadata = await updater.Invoke(state.Metadata, cts.Token).ConfigureAwait(false),
                    OwnerId = Guid.Empty,
                };
            } while (!await TryUpdateStateAsync(state, cts.Token).ConfigureAwait(false));

            return state.Identity;
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, LifetimeToken))
        {
            throw CreateException();
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts.Token)
        {
            throw new OperationCanceledException(e.Message, e, cts.CancellationOrigin);
        }
        finally
        {
            await cts.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Releases the lease ungracefully.
    /// </summary>
    /// <remarks>
    /// It's possible to call <see cref="AcquireAsync(Guid, CancellationToken)"/> method after the current one
    /// and get a new lease while the existing owner thinks that the lease is exclusively owned.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
    public ValueTask<LeaseIdentity> UnsafeReleaseAsync<TArg>(TArg arg, Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token = default)
        => updater is not null ? UnsafeReleaseAsync(new Updater<TArg>(arg, updater), token) : ValueTask.FromException<LeaseIdentity>(new ArgumentNullException(nameof(updater)));

    /// <summary>
    /// Releases the lease ungracefully.
    /// </summary>
    /// <remarks>
    /// It's possible to call <see cref="AcquireAsync(Guid, CancellationToken)"/> method after the current one
    /// and get a new lease while the existing owner thinks that the lease is exclusively owned.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<LeaseIdentity> UnsafeReleaseAsync(CancellationToken token = default)
        => UnsafeReleaseAsync(NoOpUpdater.Instance, token);

    /// <summary>
    /// Starts tracking the expiration of the lease.
    /// </summary>
    /// <remarks>
    /// The expiration of the lease is tracked from the moment when this instance observes the particular version
    /// of the lease for the first time. This method can be called immediately after the instantiation of the provider
    /// to reduce the time needed to take over the lease that was not released explicitly.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the lease is alive from the perspective of this instance;
    /// <see langword="false"/> if it's expired, released, or never acquired.
    /// The first observation of the lease that was not released explicitly always returns <see langword="true"/>.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async ValueTask<bool> ObserveAsync(CancellationToken token = default)
    {
        var cts = cancellationTokens.Combine(token, LifetimeToken);
        try
        {
            return GetRemainingTime(await GetStateAsync(cts.Token).ConfigureAwait(false)) > TimeSpan.Zero;
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, LifetimeToken))
        {
            throw CreateException();
        }
        catch (OperationCanceledException e) when (e.CancellationToken == cts.Token)
        {
            throw new OperationCanceledException(e.Message, e, cts.CancellationOrigin);
        }
        finally
        {
            await cts.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Loads the state of a lease from the underlying storage.
    /// </summary>
    /// <remarks>
    /// The method can be called concurrently.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The state restored from the underlying storage.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected abstract ValueTask<State> GetStateAsync(CancellationToken token);

    /// <summary>
    /// Attempts to update the state in the underlying storage using compare-and-set semantics.
    /// </summary>
    /// <remarks>
    /// The operation must use compare-and-set semantics in the following way: save <paramref name="state"/>
    /// only if <see cref="LeaseIdentity.Version"/> of the currently stored state is equal to the version of <paramref name="state"/> - 1.
    /// Note that the method can be called concurrently.
    /// </remarks>
    /// <param name="state">The state to be stored in the underlying storage.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if update is performed successfully; <see langword="false"/> is compare-and-set failed.</returns>
    protected abstract ValueTask<bool> TryUpdateStateAsync(State state, CancellationToken token);

    private void Terminate()
    {
        if (Interlocked.Exchange(ref lifetimeTokenSource, null) is { } cts)
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

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Terminate();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Represents a state of the lease.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct State
    {
        /// <summary>
        /// The metadata associated with the lease.
        /// </summary>
        public TMetadata Metadata { get; init; }

        /// <summary>
        /// A version of the lease.
        /// </summary>
        /// <remarks>
        /// Must be set of <see cref="LeaseIdentity.InitialVersion"/> if there is no state in the underlying persistent storage.
        /// </remarks>
        public required LeaseIdentity Identity { get; init; }

        /// <summary>
        /// A timestamp of when this state was created.
        /// </summary>
        /// <remarks>
        /// The value is informational and not used for expiration tracking, except the default value
        /// that indicates the released lease.
        /// </remarks>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// The unique identifier of the lease owner.
        /// </summary>
        /// <remarks>
        /// <see cref="Guid.Empty"/> if the lease is released or there is no state in the underlying persistent storage.
        /// </remarks>
        public required Guid OwnerId { get; init; }

        internal bool IsReleased => Identity.Version is LeaseIdentity.InitialVersion || CreatedAt == default;

        internal bool IsOwnedBy(Guid owner)
            => owner != Guid.Empty && OwnerId == owner && !IsReleased;
    }

    private TimeSpan GetRemainingTime(in State state)
    {
        TimeSpan remainingTime;
        if (state.IsReleased)
        {
            remainingTime = TimeSpan.Zero;
        }
        else
        {
            var observationCopy = observation.Value;
            if (observationCopy.Identity != state.Identity)
            {
                // Don't overwrite a concurrent observation. If it's of the same version, reuse it: the timestamp
                // of any observation never precedes the write of the observed version. Otherwise, use the local one.
                var newObservation = new Observation(state.Identity, provider);
                if (observation.CompareExchange(in newObservation, in observationCopy, out observationCopy)
                    || observationCopy.Identity != state.Identity)
                {
                    observationCopy = newObservation;
                }
            }

            remainingTime = TimeToLive - observationCopy.Timestamp.GetElapsedTime(provider);
        }

        return remainingTime;
    }

    /// <summary>
    /// Represents a result of lease acquisition operation.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct AcquisitionResult
    {
        /// <summary>
        /// A version of the lease.
        /// </summary>
        public readonly State State;

        /// <summary>
        /// The expiration timeout.
        /// </summary>
        public readonly Timeout Expiration;

        internal AcquisitionResult(in State state, TimeSpan ttl, TimeProvider provider)
        {
            Debug.Assert(ttl > TimeSpan.Zero);

            State = state;
            Expiration = new(ttl, provider);
        }

        /// <summary>
        /// Deconstructs the result.
        /// </summary>
        /// <param name="state">Same as <see cref="AcquisitionResult.State"/>.</param>
        /// <param name="lifetime">Same as <paramref name="lifetime"/>.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void Deconstruct(out State state, out Timeout lifetime)
        {
            state = State;
            lifetime = Expiration;
        }
    }
}