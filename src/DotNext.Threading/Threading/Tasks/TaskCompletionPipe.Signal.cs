namespace DotNext.Threading.Tasks;

using Pooling;

public partial class TaskCompletionPipe<T>
{
    private sealed class Signal : LinkedValueTaskCompletionSource<bool>, IPooledManualResetCompletionSource<Signal>
    {
        private Action<Signal>? completionCallback;

        protected override void AfterConsumed() => completionCallback?.Invoke(this);

        ref Action<Signal>? IPooledManualResetCompletionSource<Signal>.OnConsumed => ref completionCallback;
    }

    private ValueTaskPool<bool, Signal> pool;
    private Signal? signal;
}