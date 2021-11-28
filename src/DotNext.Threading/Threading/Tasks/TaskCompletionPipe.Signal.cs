namespace DotNext.Threading.Tasks;

using Pooling;

public partial class TaskCompletionPipe<T>
{
    private sealed class Signal : ValueTaskCompletionSource<bool>, IPooledManualResetCompletionSource<Signal>
    {
        private Action<Signal>? completionCallback;

        protected override void AfterConsumed() => completionCallback?.Invoke(this);

        private protected override void ResetCore()
        {
            completionCallback = null;
            base.ResetCore();
        }

        ref Action<Signal>? IPooledManualResetCompletionSource<Signal>.OnConsumed => ref completionCallback;
    }

    private readonly ValueTaskPool<Signal> pool;
    private Signal? signal;
}