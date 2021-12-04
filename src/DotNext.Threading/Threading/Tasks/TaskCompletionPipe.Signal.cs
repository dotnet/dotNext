using Debug = System.Diagnostics.Debug;

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
    private LinkedValueTaskCompletionSource<bool>? first, last;

    private void RemoveNode(LinkedValueTaskCompletionSource<bool> signal)
    {
        Debug.Assert(Monitor.IsEntered(this));

        if (ReferenceEquals(signal, first))
            first = signal.Next;

        if (ReferenceEquals(signal, last))
            last = signal.Previous;

        signal.Detach();
    }

    private void Notify()
    {
        for (LinkedValueTaskCompletionSource<bool>? current = first, next; current is not null; current = next)
        {
            next = current.Next;

            var signaled = current.TrySetResult(true);
            RemoveNode(current);

            if (signaled)
                break;
        }
    }

    private LinkedValueTaskCompletionSource<bool> EnqueueNode()
    {
        Debug.Assert(Monitor.IsEntered(this));

        LinkedValueTaskCompletionSource<bool> result = pool.Get();

        if (last is null)
        {
            first = last = result;
        }
        else
        {
            last.Append(result);
            last = result;
        }

        return result;
    }
}