using System.Runtime.InteropServices;

namespace DotNext.Threading.Leases;

using Diagnostics;

// The version of the lease and the local time of its first observation.
// Losing the observation is safe: the next observation starts later, so the lease expires later.
[StructLayout(LayoutKind.Auto)]
internal readonly record struct Observation(long Version, Guid RenewalStamp, Timestamp Timestamp)
{
    public Observation(long version, Guid renewalStamp, TimeProvider provider)
        : this(version, renewalStamp, new Timestamp(provider))
    {
    }

    // can be negative
    public TimeSpan GetRemainingTime(TimeSpan timeToLive, TimeProvider provider)
        => timeToLive - Timestamp.GetElapsedTime(provider);
}
