using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading.Leases;

using Diagnostics;

// The lease algorithm shared by all the lease providers
internal sealed partial class LeaseEngine<TMetadata> : IDisposable
{
    private readonly TimeProvider provider;
    private readonly CancellationTokenMultiplexer cancellationTokens;

    [SuppressMessage("Usage", "CA2213", Justification = "Disposed using Dispose() method")]
    private volatile CancellationTokenSource? lifetimeTokenSource;

    public LeaseEngine(TimeProvider? provider)
    {
        this.provider = provider ?? TimeProvider.System;
        lifetimeTokenSource = new();
        LifetimeToken = lifetimeTokenSource.Token;
        cancellationTokens = new() { MaximumRetained = 100 };
    }

    public CancellationToken LifetimeToken { get; } // cached to avoid ObjectDisposedException

    public ValueTask<LeaseAcquisitionResult<TMetadata>?> TryAcquireAsync<TStorage, TArg>(TStorage storage,
        Guid ownerId, TArg arg, Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater,
        CancellationToken token)
        where TStorage : ILeaseStorage
        => TryChangeStateAsync<TStorage, AcquisitionCondition, Updater<TArg>>(
            storage,
            new(ownerId),
            new(arg, updater),
            token);

    public ValueTask<LeaseAcquisitionResult<TMetadata>?> TryAcquireAsync<TStorage>(TStorage storage,
        Guid ownerId, CancellationToken token)
        where TStorage : ILeaseStorage
        => TryChangeStateAsync<TStorage, AcquisitionCondition, NoOpUpdater>(
            storage,
            new(ownerId),
            NoOpUpdater.Instance,
            token);

    public ValueTask<LeaseAcquisitionResult<TMetadata>?> TryRenewAsync<TStorage, TArg>(TStorage storage, Guid ownerId, bool reacquire, TArg arg,
        Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token)
        where TStorage : ILeaseStorage
        => TryChangeStateAsync<TStorage, RenewalWithUpdateCondition, Updater<TArg>>(storage,
            new(ownerId, reacquire),
            new(arg, updater), token);

    public ValueTask<LeaseAcquisitionResult<TMetadata>?> TryRenewAsync<TStorage>(TStorage storage, Guid ownerId, bool reacquire,
        CancellationToken token)
        where TStorage : ILeaseStorage
        => TryChangeStateAsync<TStorage, RenewalCondition, NoOpUpdater>(storage,
            new(ownerId, reacquire),
            NoOpUpdater.Instance, token);

    private async ValueTask<LeaseAcquisitionResult<TMetadata>?> TryChangeStateAsync<TStorage, TCondition, TUpdater>(TStorage storage,
        TCondition condition, TUpdater updater, CancellationToken token)
        where TStorage : ILeaseStorage
        where TCondition : ITransitionCondition
        where TUpdater : ISupplier<TMetadata, CancellationToken, ValueTask<TMetadata>>
    {
        var cts = cancellationTokens.Combine(token, LifetimeToken);
        try
        {
            var currentState = await storage.GetStateAsync(cts.Token).ConfigureAwait(false);

            if (!condition.Invoke(in currentState, storage.GetRemainingTime(in currentState, provider) <= TimeSpan.Zero))
                return null;
            
            var metadata = await updater.Invoke(currentState.Metadata, cts.Token).ConfigureAwait(false);

            // the timestamp must precede CreatedAt, so the reported remaining time never exceeds the stored one
            var ts = new Timestamp(provider);
            var newState = currentState with
            {
                Version = TCondition.RequiresVersionBump || currentState.RenewalStamp == Guid.Empty
                    ? currentState.Version + 1L
                    : currentState.Version,
                CreatedAt = provider.GetUtcNow(),
                Metadata = metadata,
                OwnerId = condition.OwnerId,
                RenewalStamp = currentState.NewRenewalStamp(),
            };

            if (!await storage.TryUpdateStateAsync(currentState, newState, cts.Token).ConfigureAwait(false))
                return null;

            storage.Observation = newState.ObserveAt(ts);
            var remainingTime = newState.TimeToLive - ts.GetElapsedTime(provider);
            return remainingTime > TimeSpan.Zero
                ? new LeaseAcquisitionResult<TMetadata>(in newState, remainingTime, provider)
                : null;
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, LifetimeToken))
        {
            throw new ObjectDisposedException(storage.Name);
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

    public ValueTask<LeaseAcquisitionResult<TMetadata>> AcquireAsync<TStorage, TArg>(TStorage storage, Guid ownerId, TArg arg,
        Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token)
        where TStorage : ILeaseStorage
        => AcquireAsync<TStorage, Updater<TArg>>(storage, ownerId, new(arg, updater), token);

    public ValueTask<LeaseAcquisitionResult<TMetadata>> AcquireAsync<TStorage>(TStorage storage, Guid ownerId, CancellationToken token)
        where TStorage : ILeaseStorage
        => AcquireAsync(storage, ownerId, NoOpUpdater.Instance, token);

    private async ValueTask<LeaseAcquisitionResult<TMetadata>> AcquireAsync<TStorage, TUpdater>(TStorage storage,
        Guid ownerId, TUpdater updater, CancellationToken token)
        where TStorage : ILeaseStorage
        where TUpdater : ISupplier<TMetadata, CancellationToken, ValueTask<TMetadata>>
    {
        var cts = cancellationTokens.Combine(token, LifetimeToken);
        try
        {
            while (true)
            {
                var currentState = await storage.GetStateAsync(cts.Token).ConfigureAwait(false);

                var remainingTime = storage.GetRemainingTime(in currentState, provider);
                if (remainingTime <= TimeSpan.Zero || currentState.IsOwnedBy(ownerId))
                {
                    var metadata = await updater.Invoke(currentState.Metadata, cts.Token).ConfigureAwait(false);

                    // the timestamp must precede CreatedAt, so the reported remaining time never exceeds the stored one
                    var ts = new Timestamp(provider);
                    var newState = currentState with
                    {
                        CreatedAt = provider.GetUtcNow(),
                        Version = currentState.Version + 1L,
                        Metadata = metadata,
                        OwnerId = ownerId,
                        RenewalStamp = currentState.NewRenewalStamp(),
                    };

                    if (!await storage.TryUpdateStateAsync(currentState, newState, cts.Token).ConfigureAwait(false))
                        continue;

                    storage.Observation = newState.ObserveAt(ts);
                    remainingTime = newState.TimeToLive - ts.GetElapsedTime(provider);

                    if (remainingTime <= TimeSpan.Zero)
                        continue;

                    return new LeaseAcquisitionResult<TMetadata>(in newState, remainingTime, provider);
                }

                await Task.Delay(remainingTime, provider, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, LifetimeToken))
        {
            throw new ObjectDisposedException(storage.Name);
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

    public ValueTask<long?> TryReleaseAsync<TStorage, TArg>(TStorage storage, Guid ownerId, TArg arg,
        Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token)
        where TStorage : ILeaseStorage
        => TryReleaseAsync<TStorage, Updater<TArg>>(storage, ownerId, new(arg, updater), token);

    public ValueTask<long?> TryReleaseAsync<TStorage>(TStorage storage, Guid ownerId, CancellationToken token)
        where TStorage : ILeaseStorage
        => TryReleaseAsync(storage, ownerId, NoOpUpdater.Instance, token);

    // Guid.Empty owner means unsafe release
    private async ValueTask<long?> TryReleaseAsync<TStorage, TUpdater>(TStorage storage, Guid ownerId, TUpdater updater, CancellationToken token)
        where TStorage : ILeaseStorage
        where TUpdater : ISupplier<TMetadata, CancellationToken, ValueTask<TMetadata>>
    {
        var cts = cancellationTokens.Combine(token, LifetimeToken);
        try
        {
            var currentState = await storage.GetStateAsync(cts.Token).ConfigureAwait(false);

            // the expired lease can be released, because it's not taken by another owner
            if (ownerId != Guid.Empty && !currentState.IsOwnedBy(ownerId))
                return null;

            var newState = currentState with
            {
                CreatedAt = default,
                Version = currentState.Version + 1L,
                Metadata = await updater.Invoke(currentState.Metadata, cts.Token).ConfigureAwait(false),
                OwnerId = Guid.Empty,
                RenewalStamp = currentState.NewRenewalStamp(),
            };

            return await storage.TryUpdateStateAsync(currentState, newState, cts.Token).ConfigureAwait(false)
                ? newState.Version
                : null;
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, LifetimeToken))
        {
            throw new ObjectDisposedException(storage.Name);
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

    public ValueTask<long> UnsafeReleaseAsync<TStorage, TArg>(TStorage storage, TArg arg,
        Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token)
        where TStorage : ILeaseStorage
        => UnsafeReleaseAsync<TStorage, Updater<TArg>>(storage, new(arg, updater), token);

    public ValueTask<long> UnsafeReleaseAsync<TStorage>(TStorage storage, CancellationToken token)
        where TStorage : ILeaseStorage
        => UnsafeReleaseAsync(storage, NoOpUpdater.Instance, token);

    private async ValueTask<long> UnsafeReleaseAsync<TStorage, TUpdater>(TStorage storage, TUpdater updater, CancellationToken token)
        where TStorage : ILeaseStorage
        where TUpdater : ISupplier<TMetadata, CancellationToken, ValueTask<TMetadata>>
    {
        var cts = cancellationTokens.Combine(token, LifetimeToken);
        try
        {
            LeaseState<TMetadata> currentState, newState;
            do
            {
                currentState = await storage.GetStateAsync(cts.Token).ConfigureAwait(false);

                newState = currentState with
                {
                    CreatedAt = default,
                    Version = currentState.Version + 1L,
                    Metadata = await updater.Invoke(currentState.Metadata, cts.Token).ConfigureAwait(false),
                    OwnerId = Guid.Empty,
                    RenewalStamp = currentState.NewRenewalStamp(),
                };
            } while (!await storage.TryUpdateStateAsync(currentState, newState, cts.Token).ConfigureAwait(false));

            return newState.Version;
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, LifetimeToken))
        {
            throw new ObjectDisposedException(storage.Name);
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

    public async ValueTask<(bool IsAlive, bool IsReleased)> ObserveAsync<TStorage>(TStorage storage, CancellationToken token)
        where TStorage : ILeaseStorage
    {
        var cts = cancellationTokens.Combine(token, LifetimeToken);
        try
        {
            var state = await storage.GetStateAsync(cts.Token).ConfigureAwait(false);
            return new()
            {
                IsAlive = storage.GetRemainingTime(in state, provider) > TimeSpan.Zero,
                IsReleased = state.IsReleased
            };
        }
        catch (OperationCanceledException e) when (e.CausedBy(cts, LifetimeToken))
        {
            throw new ObjectDisposedException(storage.Name);
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

    public void Dispose()
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
}