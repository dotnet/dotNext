using System.Runtime.InteropServices;

namespace DotNext.Threading.Leases;

using Diagnostics;

/// <summary>
/// Represents a state of the lease.
/// </summary>
/// <typeparam name="TMetadata">The type of metadata associated with a lease.</typeparam>
/// <seealso cref="LeaseProvider{TMetadata}"/>
/// <seealso cref="LeaseProvider{TKey, TMetadata}"/>
[StructLayout(LayoutKind.Auto)]
public readonly struct LeaseState<TMetadata>
{
    /// <summary>
    /// The metadata associated with the lease.
    /// </summary>
    public TMetadata Metadata { get; init; }

    /// <summary>
    /// A version of the lease.
    /// </summary>
    /// <remarks>
    /// The version never decreases, so it can be used as a fencing token. It is incremented on acquisition and release
    /// of the lease. If the underlying storage supports <see cref="RenewalStamp"/>, the renewal of the lease
    /// by the same owner doesn't change the version. Otherwise, the version is incremented on renewal as well.
    /// <para>
    /// The underlying storage must create a lease (including the re-creation after deletion) with the version
    /// taken from the global non-negative sequence number that never decreases, e.g., a database sequence.
    /// Otherwise, the re-created lease can reuse the versions of the deleted one, which breaks the expiration tracking,
    /// the compare-and-set semantics of the storage and fencing.
    /// If there is no state in the underlying storage, return the released state with such version.
    /// </para>
    /// </remarks>
    public required long Version { get; init; }

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

    /// <summary>
    /// Gets or sets lease duration.
    /// </summary>
    /// <remarks>
    /// The value is provided by the underlying storage, so all the providers sharing the same storage
    /// agree on the lease duration. It must be set even if there is no state in the underlying storage.
    /// <para>
    /// The underlying storage must not decrease the value while the lease is acquired, because the current owner
    /// relies on the duration it received on acquisition or renewal, while the other providers apply
    /// the decreased value immediately, and take over the lease before the owner considers it expired.
    /// The value can be decreased only when the lease is released. Increasing the value is always safe.
    /// </para>
    /// </remarks>
    /// <value>The positive lease duration.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or greater than <see cref="Timeout.MaxTimeoutParameterTicks"/>.</exception>
    public required TimeSpan TimeToLive
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Ticks, Timeout.MaxTimeoutParameterTicks, nameof(value));

            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the stamp of the latest renewal of the lease.
    /// </summary>
    /// <remarks>
    /// The stamp allows renewing the lease without incrementing <see cref="Version"/>, so the fencing token
    /// remains the same while the lease is held by the same owner, and the renewal doesn't need to be durable.
    /// The value is opaque, it is compared only for equality and must not be interpreted.
    /// <para>
    /// <see cref="Guid.Empty"/> means that the underlying storage doesn't support renewal stamps.
    /// In that case, every renewal increments <see cref="Version"/>. The renewal that updates the metadata
    /// always increments <see cref="Version"/>.
    /// </para>
    /// <para>
    /// If the underlying storage supports renewal stamps, it must return the stamp of the current state,
    /// including the released state and the state of the lease that doesn't exist, and never <see cref="Guid.Empty"/>.
    /// The compare-and-set operation must check the stamp in addition to <see cref="Version"/>, and store
    /// the stamp of the new state on success. The stamp can be kept in memory only:
    /// if it's lost, e.g., on restart, the storage must generate a new one using <see cref="Guid.NewGuid"/>.
    /// The storage must never return the stamp again after it has been replaced or lost,
    /// otherwise the providers can consider the lease expired while it's still held.
    /// </para>
    /// </remarks>
    public Guid RenewalStamp { get; init; }

    internal bool IsReleased => CreatedAt == default;

    internal bool IsOwnedBy(Guid owner)
        => owner != Guid.Empty && OwnerId == owner && !IsReleased;

    internal Observation ObserveAt(Timestamp ts) => new(Version, RenewalStamp, ts);

    internal Observation ObserveNow(TimeProvider provider) => new(Version, RenewalStamp, provider);

    internal bool IsObservedBy(in Observation observation)
        => Version == observation.Version && RenewalStamp == observation.RenewalStamp;

    // the storage that doesn't support renewal stamps keeps Guid.Empty
    internal Guid NewRenewalStamp() => RenewalStamp == Guid.Empty ? Guid.Empty : Guid.NewGuid();
}
