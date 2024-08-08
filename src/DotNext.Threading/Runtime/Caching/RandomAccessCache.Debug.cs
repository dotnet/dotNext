using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime.Caching;

[DebuggerDisplay($"EvictionListSize = {{{nameof(EvictionListSize)}}}")]
public partial class RandomAccessCache<TKey, TValue>
{
    [ExcludeFromCodeCoverage]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private (int Dead, int Alive) EvictionListSize => evictionHead?.EvictionNodesCount ?? default;

    internal partial class KeyValuePair
    {
        protected string ToString(TValue value) => $"Key = {Key} Value = {value}, Promoted = {Task.IsCompleted}, IsAlive = {!IsDead}";
    }
}