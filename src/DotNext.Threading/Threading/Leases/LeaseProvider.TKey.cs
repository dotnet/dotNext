using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Leases;

using static Runtime.ExceptionServices.ExceptionExtensions;

/// <summary>
/// Represents provider side of multiple leases in a distributed environment.
/// </summary>
/// <remarks>
/// An instance of this type must support concurrent calls. Each lease is identified by the key
/// and tracked independently in the same way as by <see cref="LeaseProvider{TMetadata}"/>.
/// <para>
/// Each instance tracks the expiration of every lease using its own monotonic clock, starting from the moment it observes
/// a particular version of the lease. Thus, the wall clocks of the instances sharing the same storage don't need
/// to be synchronized. As a consequence, a newly created instance waits for the full <see cref="LeaseState{TMetadata}.TimeToLive"/>
/// before taking over the lease that was not released explicitly. Call <see cref="ObserveAsync(TKey, CancellationToken)"/>
/// immediately after the instantiation to start tracking the expiration as early as possible.
/// </para>
/// <para>
/// The provider keeps the tracking information for every lease it has observed, until it observes the lease released.
/// </para>
/// <para>
/// The lease is held on behalf of the owner identified by <see cref="Guid"/>. The owner must be unique
/// across all consumers of the lease. Acquisition, renewal and release by the owner don't require the actual
/// version of the lease. Thus, these operations can be retried safely if the response is lost.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of the lease identifier.</typeparam>
/// <typeparam name="TMetadata">The type of metadata associated with a lease.</typeparam>
/// <seealso cref="LeaseConsumer"/>
/// <seealso cref="LeaseProvider{TMetadata}"/>
public abstract class LeaseProvider<TKey, TMetadata> : Disposable
    where TKey : notnull
{
    private readonly LeaseEngine<TMetadata> engine;
    private readonly ObservationDictionary observations;

    /// <summary>
    /// Initializes a new instance of lease provider.
    /// </summary>
    /// <param name="comparer">The comparer of the lease identifiers.</param>
    /// <param name="provider">The time provider.</param>
    protected LeaseProvider(IEqualityComparer<TKey>? comparer = null, TimeProvider? provider = null)
    {
        engine = new(provider);
        observations = new(comparer);
    }

    /// <summary>
    /// A token that represents state of this object.
    /// </summary>
    /// <remarks>
    /// A call to <see cref="Dispose(bool)"/> cancels the token.
    /// </remarks>
    protected CancellationToken LifetimeToken => engine.LifetimeToken;

    private Storage AsStorage(TKey leaseId) => new(this, leaseId);

    private bool RemoveObservation(TKey leaseId) => observations
        .As<IDictionary<TKey, Observation>>()
        .Remove(leaseId);

    /// <summary>
    /// Drops the tracking information of the lease.
    /// </summary>
    /// <remarks>
    /// The provider keeps the tracking information for every lease it has observed, until it observes the lease released.
    /// This method allows dropping it without the observation, e.g., when the lease is deleted from the underlying storage.
    /// If the lease is observed after this call, the expiration of its current version is tracked from that moment.
    /// </remarks>
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <returns><see langword="true"/> if the tracking information has been dropped; <see langword="false"/> if the lease was not tracked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="leaseId"/> is <see langword="null"/>.</exception>
    protected bool Forget(TKey leaseId)
    {
        ArgumentNullException.ThrowIfNull<TKey>(leaseId);

        return RemoveObservation(leaseId);
    }

    private static ArgumentNullException? CheckArguments(TKey? leaseId,
        Delegate? updater,
        [CallerArgumentExpression(nameof(leaseId))]
        string leaseIdParamName = "",
        [CallerArgumentExpression(nameof(updater))]
        string updaterParamName = "")
        => leaseId is null
            ? new ArgumentNullException(leaseIdParamName)
            : updater is null
                ? new ArgumentNullException(updaterParamName)
                : null;

    private static ArgumentException? CheckArguments(
        TKey? leaseId,
        Guid ownerId,
        [CallerArgumentExpression(nameof(leaseId))]
        string leaseIdParamName = "",
        [CallerArgumentExpression(nameof(ownerId))]
        string ownerIdParamName = "")
        => leaseId is null
            ? new ArgumentNullException(leaseIdParamName)
            : LeaseEngine.CheckArguments(ownerId, ownerIdParamName);

    private static ArgumentException? CheckArguments(TKey? leaseId,
        Guid ownerId,
        Delegate? updater,
        [CallerArgumentExpression(nameof(leaseId))]
        string leaseIdParamName = "",
        [CallerArgumentExpression(nameof(ownerId))]
        string ownerIdParamName = "",
        [CallerArgumentExpression(nameof(updater))]
        string updaterParamName = "")
        => leaseId is null
            ? new ArgumentNullException(leaseIdParamName)
            : LeaseEngine.CheckArguments(ownerId, updater, ownerIdParamName, updaterParamName);

    /// <summary>
    /// Tries to acquire the lease.
    /// </summary>
    /// <remarks>
    /// If the lease is already held by <paramref name="ownerId"/>, it is renewed.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The acquisition result; or <see langword="null"/> if the lease is taken by another owner.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="leaseId"/> or <paramref name="updater"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<LeaseAcquisitionResult<TMetadata>?> TryAcquireAsync<TArg>(TKey leaseId, Guid ownerId, TArg arg,
        Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token = default)
        => CheckArguments(leaseId, ownerId, updater) is { } e
            ? ValueTask.FromException<LeaseAcquisitionResult<TMetadata>?>(e)
            : engine.TryAcquireAsync(AsStorage(leaseId), ownerId, arg, updater, token);

    /// <summary>
    /// Tries to acquire the lease.
    /// </summary>
    /// <remarks>
    /// If the lease is already held by <paramref name="ownerId"/>, it is renewed.
    /// </remarks>
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The acquisition result; or <see langword="null"/> if the lease is taken by another owner.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="leaseId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<LeaseAcquisitionResult<TMetadata>?> TryAcquireAsync(TKey leaseId, Guid ownerId, CancellationToken token = default)
        => CheckArguments(leaseId, ownerId) is { } e
            ? ValueTask.FromException<LeaseAcquisitionResult<TMetadata>?>(e)
            : engine.TryAcquireAsync(AsStorage(leaseId), ownerId, token);

    /// <summary>
    /// Acquires the lease.
    /// </summary>
    /// <remarks>
    /// If the lease is already held by <paramref name="ownerId"/>, it is renewed.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="leaseId"/> or <paramref name="updater"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<LeaseAcquisitionResult<TMetadata>> AcquireAsync<TArg>(TKey leaseId, Guid ownerId, TArg arg,
        Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token = default)
        => CheckArguments(leaseId, ownerId, updater) is { } e
            ? ValueTask.FromException<LeaseAcquisitionResult<TMetadata>>(e)
            : engine.AcquireAsync(AsStorage(leaseId), ownerId, arg, updater, token);

    /// <summary>
    /// Acquires the lease.
    /// </summary>
    /// <remarks>
    /// If the lease is already held by <paramref name="ownerId"/>, it is renewed.
    /// </remarks>
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="leaseId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<LeaseAcquisitionResult<TMetadata>> AcquireAsync(TKey leaseId, Guid ownerId, CancellationToken token = default)
        => CheckArguments(leaseId, ownerId) is { } e
            ? ValueTask.FromException<LeaseAcquisitionResult<TMetadata>>(e)
            : engine.AcquireAsync(AsStorage(leaseId), ownerId, token);

    /// <summary>
    /// Tries to renew the lease and update its metadata.
    /// </summary>
    /// <remarks>
    /// Unlike the renewal without the metadata updater, this method always increments <see cref="LeaseState{TMetadata}.Version"/>,
    /// even if the underlying storage supports <see cref="LeaseState{TMetadata}.RenewalStamp"/>, because the metadata update
    /// is a durable change of the state. As a result, the owner gets a new fencing token.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="reacquire"><see langword="true"/> to acquire the lease on renewal if it is expired.</param>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The status of the operation; or <see langword="null"/> if the lease is taken by another owner, released, or expired.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="leaseId"/> or <paramref name="updater"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<LeaseAcquisitionResult<TMetadata>?> TryRenewAsync<TArg>(TKey leaseId, Guid ownerId, bool reacquire, TArg arg,
        Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token = default)
        => CheckArguments(leaseId, ownerId, updater) is { } e
            ? ValueTask.FromException<LeaseAcquisitionResult<TMetadata>?>(e)
            : engine.TryRenewAsync(AsStorage(leaseId), ownerId, reacquire,
                arg, updater, token);

    /// <summary>
    /// Tries to renew the lease.
    /// </summary>
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="reacquire"><see langword="true"/> to acquire the lease on renewal if it is expired.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The status of the operation; or <see langword="null"/> if the lease is taken by another owner, released, or expired.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="leaseId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public ValueTask<LeaseAcquisitionResult<TMetadata>?> TryRenewAsync(TKey leaseId, Guid ownerId, bool reacquire, CancellationToken token = default)
        => CheckArguments(leaseId, ownerId) is { } e
            ? ValueTask.FromException<LeaseAcquisitionResult<TMetadata>?>(e)
            : engine.TryRenewAsync(AsStorage(leaseId), ownerId, reacquire, token);

    /// <summary>
    /// Releases the lease.
    /// </summary>
    /// <remarks>
    /// The expired lease can be released as well if it is not taken by another owner.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The version of the released lease; or <see langword="null"/> if the lease is taken by another owner or already released.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="leaseId"/> or <paramref name="updater"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public async ValueTask<long?> ReleaseAsync<TArg>(TKey leaseId, Guid ownerId, TArg arg,
        Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token = default)
    {
        CheckArguments(leaseId, ownerId, updater)?.Throw();

        var version = await engine.TryReleaseAsync(AsStorage(leaseId), ownerId, arg, updater, token)
            .ConfigureAwait(false);
        if (version.HasValue)
            RemoveObservation(leaseId);

        return version;
    }

    /// <summary>
    /// Releases the lease.
    /// </summary>
    /// <remarks>
    /// The expired lease can be released as well if it is not taken by another owner.
    /// </remarks>
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <param name="ownerId">The unique identifier of the lease owner.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The version of the released lease; or <see langword="null"/> if the lease is taken by another owner or already released.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="leaseId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="ownerId"/> is <see cref="Guid.Empty"/>.</exception>
    public async ValueTask<long?> ReleaseAsync(TKey leaseId, Guid ownerId, CancellationToken token = default)
    {
        CheckArguments(leaseId, ownerId)?.Throw();

        var version = await engine.TryReleaseAsync(AsStorage(leaseId), ownerId, token)
            .ConfigureAwait(false);
        
        if (version.HasValue)
            RemoveObservation(leaseId);

        return version;
    }

    /// <summary>
    /// Tries to release the lease ungracefully.
    /// </summary>
    /// <remarks>
    /// It's possible to call <see cref="AcquireAsync(TKey, Guid, CancellationToken)"/> method after the current one
    /// and get a new lease while the existing owner thinks that the lease is exclusively owned.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The version of the released lease; or <see langword="null"/> if updated or taken by another process.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="leaseId"/> or <paramref name="updater"/> is <see langword="null"/>.</exception>
    public async ValueTask<long?> UnsafeTryReleaseAsync<TArg>(TKey leaseId, TArg arg,
        Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token = default)
    {
        CheckArguments(leaseId, updater)?.Throw();

        var version = await engine.TryReleaseAsync(AsStorage(leaseId), Guid.Empty, arg, updater, token)
            .ConfigureAwait(false);

        if (version.HasValue)
            RemoveObservation(leaseId);

        return version;
    }

    /// <summary>
    /// Tries to release the lease ungracefully.
    /// </summary>
    /// <remarks>
    /// It's possible to call <see cref="AcquireAsync(TKey, Guid, CancellationToken)"/> method after the current one
    /// and get a new lease while the existing owner thinks that the lease is exclusively owned.
    /// </remarks>
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The version of the released lease; or <see langword="null"/> if updated or taken by another process.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="leaseId"/> is <see langword="null"/>.</exception>
    public async ValueTask<long?> UnsafeTryReleaseAsync(TKey leaseId, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull<TKey>(leaseId);

        var version = await engine.TryReleaseAsync(AsStorage(leaseId), Guid.Empty, token)
            .ConfigureAwait(false);

        if (version.HasValue)
            RemoveObservation(leaseId);

        return version;
    }

    /// <summary>
    /// Releases the lease ungracefully.
    /// </summary>
    /// <remarks>
    /// It's possible to call <see cref="AcquireAsync(TKey, Guid, CancellationToken)"/> method after the current one
    /// and get a new lease while the existing owner thinks that the lease is exclusively owned.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to the metadata updater.</typeparam>
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <param name="arg">The argument to be passed to the metadata updater.</param>
    /// <param name="updater">An idempotent operation to update the metadata on successful acquisition of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The version of the released lease.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="leaseId"/> or <paramref name="updater"/> is <see langword="null"/>.</exception>
    public async ValueTask<long> UnsafeReleaseAsync<TArg>(TKey leaseId, TArg arg,
        Func<TArg, TMetadata, CancellationToken, ValueTask<TMetadata>> updater, CancellationToken token = default)
    {
        CheckArguments(leaseId, updater)?.Throw();

        var version = await engine.UnsafeReleaseAsync(AsStorage(leaseId), arg, updater, token)
            .ConfigureAwait(false);
        RemoveObservation(leaseId);
        return version;
    }

    /// <summary>
    /// Releases the lease ungracefully.
    /// </summary>
    /// <remarks>
    /// It's possible to call <see cref="AcquireAsync(TKey, Guid, CancellationToken)"/> method after the current one
    /// and get a new lease while the existing owner thinks that the lease is exclusively owned.
    /// </remarks>
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The version of the released lease.</returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="leaseId"/> is <see langword="null"/>.</exception>
    public async ValueTask<long> UnsafeReleaseAsync(TKey leaseId, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull<TKey>(leaseId);

        var version = await engine.UnsafeReleaseAsync(AsStorage(leaseId), token)
            .ConfigureAwait(false);
        
        RemoveObservation(leaseId);
        return version;
    }

    /// <summary>
    /// Starts tracking the expiration of the lease.
    /// </summary>
    /// <remarks>
    /// The expiration of the lease is tracked from the moment when this instance observes the particular version
    /// of the lease for the first time. This method can be called immediately after the instantiation of the provider
    /// to reduce the time needed to take over the lease that was not released explicitly.
    /// </remarks>
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the lease is alive from the perspective of this instance;
    /// <see langword="false"/> if it's expired, released, or never acquired.
    /// The first observation of the lease that was not released explicitly always returns <see langword="true"/>.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The provider has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="leaseId"/> is <see langword="null"/>.</exception>
    public async ValueTask<bool> ObserveAsync(TKey leaseId, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull<TKey>(leaseId);

        var (alive, released) = await engine.ObserveAsync(AsStorage(leaseId), token).ConfigureAwait(false);
        if (released)
            RemoveObservation(leaseId);

        return alive;
    }

    /// <summary>
    /// Loads the state of a lease from the underlying storage.
    /// </summary>
    /// <remarks>
    /// The method can be called concurrently.
    /// </remarks>
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The state restored from the underlying storage.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected abstract ValueTask<LeaseState<TMetadata>> GetStateAsync(TKey leaseId, CancellationToken token);

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
    /// <param name="leaseId">The identifier of the lease.</param>
    /// <param name="expected">The state previously returned by <see cref="GetStateAsync(TKey, CancellationToken)"/>.</param>
    /// <param name="update">The state to be stored in the underlying storage.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if update is performed successfully; <see langword="false"/> is compare-and-set failed.</returns>
    protected abstract ValueTask<bool> TryUpdateStateAsync(TKey leaseId, LeaseState<TMetadata> expected, LeaseState<TMetadata> update,
        CancellationToken token);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            engine.Dispose();
            observations.Clear();
        }

        base.Dispose(disposing);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct Storage(LeaseProvider<TKey, TMetadata> provider, TKey leaseId) : LeaseEngine<TMetadata>.ILeaseStorage
    {
        ValueTask<LeaseState<TMetadata>> LeaseEngine<TMetadata>.ILeaseStorage.GetStateAsync(CancellationToken token)
            => provider.GetStateAsync(leaseId, token);

        ValueTask<bool> LeaseEngine<TMetadata>.ILeaseStorage.TryUpdateStateAsync(LeaseState<TMetadata> expected,
            LeaseState<TMetadata> update, CancellationToken token)
            => provider.TryUpdateStateAsync(leaseId, expected, update, token);

        Observation LeaseEngine<TMetadata>.ILeaseStorage.Observation
        {
            set => provider.observations[leaseId] = value;
        }

        TimeSpan LeaseEngine<TMetadata>.ILeaseStorage.GetRemainingTime(in LeaseState<TMetadata> state, TimeProvider timeProvider)
            => provider.observations.GetRemainingTime(leaseId, in state, timeProvider);

        string LeaseEngine<TMetadata>.ILeaseStorage.Name => provider.GetType().Name;
    }
    
    private sealed class ObservationDictionary(IEqualityComparer<TKey>? comparer) : ConcurrentDictionary<TKey, Observation>(comparer)
    {
        public TimeSpan GetRemainingTime(TKey leaseId, in LeaseState<TMetadata> state, TimeProvider provider)
        {
            if (state.IsReleased)
                return TimeSpan.Zero;

            var exists = TryGetValue(leaseId, out var observation);
            if (!exists || !state.IsObservedBy(in observation))
            {
                // emulates compare-and-exchange
                var newObservation = state.ObserveNow(provider);
                if ((exists
                        ? TryUpdate(leaseId, newObservation, observation)
                        : TryAdd(leaseId, newObservation))
                    || !TryGetValue(leaseId, out observation)
                    || !state.IsObservedBy(in observation))
                {
                    observation = newObservation;
                }
            }

            return observation.GetRemainingTime(state.TimeToLive, provider);
        }
    }
}