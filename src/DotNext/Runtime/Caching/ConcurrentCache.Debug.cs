using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime.Caching;

[DebuggerDisplay($"Count = {{{nameof(Count)}}}, CommandQueueSize = {{{nameof(CommandQueueSize)}}}, CommandPoolSize = {{{nameof(CommandPoolSize)}}}")]
public partial class ConcurrentCache<TKey, TValue>
{
    [ExcludeFromCodeCoverage]
    private int CommandQueueSize
    {
        get
        {
            var result = 0;

            for (var current = commandQueueReadPosition.Next; current is not null; current = current.Next)
                result++;

            return result;
        }
    }

    [ExcludeFromCodeCoverage]
    private int CommandPoolSize
    {
        get
        {
            var result = 0;

            for (var current = pool; current is not null; current = current.Next)
                result++;

            return result;
        }
    }
}