using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime.Caching;

[DebuggerDisplay($"EvictionListSize = {{{nameof(EvictionListSize)}}}")]
public partial class RandomAccessCache<TKey, TValue>
{
    [ExcludeFromCodeCoverage]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int EvictionListSize => evictionHead?.LinkedNodesCount ?? 0;
}