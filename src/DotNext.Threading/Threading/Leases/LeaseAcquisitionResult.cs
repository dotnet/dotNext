using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Leases;

/// <summary>
/// Represents a result of lease acquisition operation.
/// </summary>
/// <typeparam name="TMetadata">The type of metadata associated with a lease.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct LeaseAcquisitionResult<TMetadata>
{
    /// <summary>
    /// The state of the lease.
    /// </summary>
    public readonly LeaseState<TMetadata> State;

    /// <summary>
    /// The expiration timeout.
    /// </summary>
    public readonly Timeout Expiration;

    internal LeaseAcquisitionResult(in LeaseState<TMetadata> state, TimeSpan ttl, TimeProvider provider)
    {
        Debug.Assert(ttl > TimeSpan.Zero);

        State = state;
        Expiration = new(ttl, provider);
    }

    /// <summary>
    /// Deconstructs the result.
    /// </summary>
    /// <param name="state">Same as <see cref="State"/>.</param>
    /// <param name="lifetime">Same as <see cref="Expiration"/>.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Deconstruct(out LeaseState<TMetadata> state, out Timeout lifetime)
    {
        state = State;
        lifetime = Expiration;
    }
}
