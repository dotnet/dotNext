using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.Caching;

[StructLayout(LayoutKind.Auto)]
internal struct SieveState
{
    private const sbyte EvictedState = -1;
    private const sbyte NotVisitedState = 0;
    private const sbyte VisitedState = 1;

    // Any negative value means evicted. TryEvict can decrement an evicted state once more, but never below -2:
    // only the eviction thread calls it, and the entry leaves the SIEVE list as soon as it returns true.
    // A further decrement would eventually wrap the sbyte around to a positive (alive) state
    private volatile sbyte state;

    // Gives the visited entry a second chance, or evicts it, in one atomic step, so a concurrent visit is never lost.
    // Returns false for the second chance. Otherwise, removed is true if the entry was evicted before this call
    public bool TryEvict(out bool removed)
    {
        var current = state;
        var actual = Interlocked.CompareExchange(ref state, (sbyte)(current - 1), current);
        return actual == current
            ? IsEvicted(current, out removed)
            : TryEvictSlow(actual, out removed);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryEvictSlow(sbyte current, out bool removed)
    {
        for (sbyte tmp;; current = tmp)
        {
            tmp = Interlocked.CompareExchange(ref state, (sbyte)(current - 1), current);
            if (tmp == current)
                break;
        }

        return IsEvicted(current, out removed);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsEvicted(sbyte current, out bool removed)
    {
        removed = current < NotVisitedState;
        return current <= NotVisitedState;
    }

    public bool Visit() => state switch
    {
        VisitedState => true,
        NotVisitedState => Interlocked.CompareExchange(ref state, VisitedState, NotVisitedState) >= NotVisitedState,
        _ => false,
    };

    // true if the entry was alive
    public bool MarkAsEvicted()
        => Interlocked.Exchange(ref state, EvictedState) >= NotVisitedState;

    public readonly bool IsDead => state < NotVisitedState;

    [ExcludeFromCodeCoverage]
    public readonly override string ToString() => state switch
    {
        NotVisitedState => "NotVisited",
        VisitedState => "Visited",
        _ => "Evicted",
    };
}
