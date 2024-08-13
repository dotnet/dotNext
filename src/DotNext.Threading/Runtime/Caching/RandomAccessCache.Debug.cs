using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime.Caching;

[DebuggerDisplay($"EvictionListSize = {{{nameof(EvictionListSize)}}}, QueueSize = {{{nameof(QueueSize)}}}")]
public partial class RandomAccessCache<TKey, TValue>
{
    [ExcludeFromCodeCoverage]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private (int Dead, int Alive) EvictionListSize => evictionHead?.EvictionNodesCount ?? default;

    [ExcludeFromCodeCoverage]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int QueueSize => queueHead?.QueueLength ?? 0;

    internal partial class KeyValuePair
    {
        protected string ToString(TValue value) => $"Key = {Key} Value = {value}, Promoted = {IsNotified}, IsAlive = {!IsDead}";
    }
}