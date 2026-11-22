namespace DotNext.Threading.Leases;

/// <summary>
/// Represents provider side of a lease in a distributed environment.
/// </summary>
/// <remarks>
/// An instance of this type must support concurrent calls.
/// <para>
/// Each instance tracks the lease expiration using its own monotonic clock, starting from the moment it observes
/// a particular version of the lease. Thus, the wall clocks of the instances sharing the same storage don't need
/// to be synchronized. As a consequence, a newly created instance waits for the full <see cref="LeaseState{TMetadata}.TimeToLive"/>
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
/// <seealso cref="LeaseProvider{TKey, TMetadata}"/>
public abstract class LeaseProvider<TMetadata> : Disposable, LeaseEngine<TMetadata>.ILeaseStorage
{
    private readonly LeaseEngine<TMetadata> engine;
    private Atomic<Observation> observation;

    /// <summary>
    /// Initializes a new instance of lease provider.
    /// </summary>
    /// <param name="provider">The time provider.</param>
    protected LeaseProvider(TimeProvider? provider = null)
        => engine = new(provider);

    /// <summary>
    /// A token that represents state of this object.
    /// </summary>
    /// <remarks>
    /// A call to <see cref="Dispose(bool)"/> cancels the token.
    /// </remarks>
    protected CancellationToken LifetimeToken => engine.LifetimeToken;

    /// <summary>
    /// Tries to acquire the lease.
    /// </summary>
    /// <remarks>
    /// If the lease is already held by <paramref name="ownerId"/>, it is renewed.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The acquisition result; or <see langword="null"/> if the lease is taken by another owner.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<LeaseAcquisitionResult<TMetadata>?> TryAcquireAsync<TArg>(Guid ownerId, TArg arg,
        Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token = default)
        => LeaseEngine.CheckArguments(ownerId, updater) is { } e
            ? ValueTask.FromException<LeaseAcquisitionResult<TMetadata>?>(e)
            : engine.TryAcquireAsync(this, ownerId, arg, updater, token);

    /// <summary>
    /// Tries to acquire the lease.
    /// </summary>
    /// <remarks>
    /// If the lease is already held by <paramref name="ownerId"/>, it is renewed.
    /// </remarks>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The acquisition result; or <see langword="null"/> if the lease is taken by another owner.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<LeaseAcquisitionResult<TMetadata>?> TryAcquireAsync(Guid ownerId, CancellationToken token = default)
        => LeaseEngine.CheckArguments(ownerId) is { } e
            ? ValueTask.FromException<LeaseAcquisitionResult<TMetadata>?>(e)
            : engine.TryAcquireAsync(this, ownerId, token);

    /// <summary>
    /// Acquires the lease.
    /// </summary>
    /// <remarks>
    /// If the lease is already held by <paramref name="ownerId"/>, it is renewed.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<LeaseAcquisitionResult<TMetadata>> AcquireAsync<TArg>(Guid ownerId, TArg arg,
        Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token = default)
        => LeaseEngine.CheckArguments(ownerId, updater) is { } e
            ? ValueTask.FromException<LeaseAcquisitionResult<TMetadata>>(e)
            : engine.AcquireAsync(this, ownerId, arg, updater, token);

    /// <summary>
    /// Acquires the lease.
    /// </summary>
    /// <remarks>
    /// If the lease is already held by <paramref name="ownerId"/>, it is renewed.
    /// </remarks>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<LeaseAcquisitionResult<TMetadata>> AcquireAsync(Guid ownerId, CancellationToken token = default)
        => LeaseEngine.CheckArguments(ownerId) is { } e
            ? ValueTask.FromException<LeaseAcquisitionResult<TMetadata>>(e)
            : engine.AcquireAsync(this, ownerId, token);

    /// <summary>
    /// Tries to renew the lease.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="reacquire"><see langword="true"/> to acquire the lease on renewal if it is expired.</param>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The status of the operation; or <see langword="null"/> if the lease is taken by another owner, released, or expired.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<LeaseAcquisitionResult<TMetadata>?> TryRenewAsync<TArg>(Guid ownerId, bool reacquire, TArg arg,
        Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token = default)
        => LeaseEngine.CheckArguments(ownerId, updater) is { } e
            ? ValueTask.FromException<LeaseAcquisitionResult<TMetadata>?>(e)
            : engine.TryRenewAsync(this, ownerId, reacquire, arg, updater, token);

    /// <summary>
    /// Tries to renew the lease.
    /// </summary>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="reacquire"><see langword="true"/> to acquire the lease on renewal if it is expired.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The status of the operation; or <see langword="null"/> if the lease is taken by another owner, released, or expired.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<LeaseAcquisitionResult<TMetadata>?> TryRenewAsync(Guid ownerId, bool reacquire, CancellationToken token = default)
        => LeaseEngine.CheckArguments(ownerId) is { } e
            ? ValueTask.FromException<LeaseAcquisitionResult<TMetadata>?>(e)
            : engine.TryRenewAsync(this, ownerId, reacquire, token);

    /// <summary>
    /// Releases the lease.
    /// </summary>
    /// <remarks>
    /// The expired lease can be released as well if it is not taken by another owner.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The version of the released lease; or <see langword="null"/> if the lease is taken by another owner or already released.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<long?> ReleaseAsync<TArg>(Guid ownerId, TArg arg, Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater,
        CancellationToken token = default)
        => LeaseEngine.CheckArguments(ownerId, updater) is { } e
            ? ValueTask.FromException<long?>(e)
            : engine.TryReleaseAsync(this, ownerId, arg, updater, token);

    /// <summary>
    /// Releases the lease.
    /// </summary>
    /// <remarks>
    /// The expired lease can be released as well if it is not taken by another owner.
    /// </remarks>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The version of the released lease; or <see langword="null"/> if the lease is taken by another owner or already released.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<long?> ReleaseAsync(Guid ownerId, CancellationToken token = default)
        => LeaseEngine.CheckArguments(ownerId) is { } e
            ? ValueTask.FromException<long?>(e)
            : engine.TryReleaseAsync(this, ownerId, token);

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
    /// <returns>The version of the released lease; or <see langword="null"/> if updated or taken by another process.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
    public ValueTask<long?> UnsafeTryReleaseAsync<TArg>(TArg arg, Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater,
        CancellationToken token = default)
        => updater is not null
            ? engine.TryReleaseAsync(this, Guid.Empty, arg, updater, token)
            : ValueTask.FromException<long?>(new ArgumentNullException(nameof(updater)));

    /// <summary>
    /// Tries to release the lease ungracefully.
    /// </summary>
    /// <remarks>
    /// It's possible to call <see cref="AcquireAsync(Guid, CancellationToken)"/> method after the current one
    /// and get a new lease while the existing owner thinks that the lease is exclusively owned.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The version of the released lease; or <see langword="null"/> if updated or taken by another process.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<long?> UnsafeTryReleaseAsync(CancellationToken token = default)
        => engine.TryReleaseAsync(this, Guid.Empty, token);

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
    /// <returns>The version of the released lease.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
    public ValueTask<long> UnsafeReleaseAsync<TArg>(TArg arg, Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater,
        CancellationToken token = default)
        => updater is not null
            ? engine.UnsafeReleaseAsync(this, arg, updater, token)
            : ValueTask.FromException<long>(new ArgumentNullException(nameof(updater)));

    /// <summary>
    /// Releases the lease ungracefully.
    /// </summary>
    /// <remarks>
    /// It's possible to call <see cref="AcquireAsync(Guid, CancellationToken)"/> method after the current one
    /// and get a new lease while the existing owner thinks that the lease is exclusively owned.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The version of the released lease.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<long> UnsafeReleaseAsync(CancellationToken token = default)
        => engine.UnsafeReleaseAsync(this, token);

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
        => (await engine.ObserveAsync(this, token).ConfigureAwait(false)).IsAlive;

    /// <summary>
    /// Loads the state of a lease from the underlying storage.
    /// </summary>
    /// <remarks>
    /// The method can be called concurrently.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The state restored from the underlying storage.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected abstract ValueTask<LeaseState<TMetadata>> GetStateAsync(CancellationToken token);

    /// <summary>
    /// Attempts to update the state in the underlying storage using compare-and-set semantics.
    /// </summary>
    /// <remarks>
    /// The operation must use compare-and-set semantics in the following way: save <paramref name="update"/>
    /// only if <see cref="LeaseState{TMetadata}.Version"/> and <see cref="LeaseState{TMetadata}.RenewalStamp"/>
    /// of the currently stored state are equal to the corresponding properties of <paramref name="expected"/>.
    /// Other properties must not be compared.
    /// If the lease doesn't exist in the storage, save <paramref name="update"/> only if the lease is still absent.
    /// Note that the method can be called concurrently.
    /// </remarks>
    /// <param name="expected">The state previously returned by <see cref="GetStateAsync(CancellationToken)"/>.</param>
    /// <param name="update">The state to be stored in the underlying storage.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if update is performed successfully; <see langword="false"/> is compare-and-set failed.</returns>
    protected abstract ValueTask<bool> TryUpdateStateAsync(LeaseState<TMetadata> expected, LeaseState<TMetadata> update,
        CancellationToken token);
    
    ValueTask<LeaseState<TMetadata>> LeaseEngine<TMetadata>.ILeaseStorage.GetStateAsync(CancellationToken token)
        => GetStateAsync(token);

    ValueTask<bool> LeaseEngine<TMetadata>.ILeaseStorage.TryUpdateStateAsync(LeaseState<TMetadata> expected,
        LeaseState<TMetadata> update, CancellationToken token)
        => TryUpdateStateAsync(expected, update, token);

    Observation LeaseEngine<TMetadata>.ILeaseStorage.Observation
    {
        set => observation.Write(in value);
    }

    TimeSpan LeaseEngine<TMetadata>.ILeaseStorage.GetRemainingTime(in LeaseState<TMetadata> state, TimeProvider timeProvider)
    {
        if (state.IsReleased)
            return TimeSpan.Zero;
        
        var observationCopy = observation.Value;
        if (!state.IsObservedBy(in observationCopy))
        {
            var newObservation = state.ObserveNow(timeProvider);
            if (observation.CompareExchange(in newObservation, in observationCopy, out observationCopy)
                || !state.IsObservedBy(in observationCopy))
            {
                observationCopy = newObservation;
            }
        }

        return observationCopy.GetRemainingTime(state.TimeToLive, timeProvider);
    }

    string LeaseEngine<TMetadata>.ILeaseStorage.Name => GetType().Name;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            engine.Dispose();
        }

        base.Dispose(disposing);
    }
}