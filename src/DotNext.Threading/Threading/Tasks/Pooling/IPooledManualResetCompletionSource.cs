using System.Threading.Tasks.Sources;

namespace DotNext.Threading.Tasks.Pooling;

internal interface IPooledManualResetCompletionSource<TCallback> : IValueTaskSource, ISupplier<TCallback?>
    where TCallback : MulticastDelegate
{
    TCallback? OnConsumed
    {
        get;
        set;
    }

    TCallback? ISupplier<TCallback?>.Invoke() => OnConsumed;
}