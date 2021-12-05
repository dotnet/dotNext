using System.Threading.Tasks.Sources;

namespace DotNext.Threading.Tasks.Pooling;

internal interface IPooledManualResetCompletionSource<TCallback> : IValueTaskSource
    where TCallback : MulticastDelegate
{
    ref TCallback? OnConsumed { get; }
}