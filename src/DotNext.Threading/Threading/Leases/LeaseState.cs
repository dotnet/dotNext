using System.Runtime.InteropServices;

namespace DotNext.Threading.Leases;

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
    /// The version is incremented on every change of the state and never decreases, so it can be used
    /// as a fencing token.
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

    internal bool IsReleased => CreatedAt == default;

    internal bool IsOwnedBy(Guid owner)
        => owner != Guid.Empty && OwnerId == owner && !IsReleased;
}
