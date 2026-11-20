using System.Runtime.InteropServices;

namespace DotNext.Threading.Leases;

using Diagnostics;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct Observation(long Version, Timestamp Timestamp)
{
    public Observation(long version, TimeProvider provider)
        : this(version, new Timestamp(provider))
    {
    }
}