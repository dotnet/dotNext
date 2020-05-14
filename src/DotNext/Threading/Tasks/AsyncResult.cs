using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
    internal sealed class AsyncResult : IAsyncResult
    {
        private readonly Task task;
        private readonly AsyncCallback? callback;
        private readonly object? state;
        private readonly bool completedSynchronously;

        internal AsyncResult(Task task, AsyncCallback? callback, object? state)
        {
            Debug.Assert(!(task is null));

            this.task = task;
            this.state = state;

            if (completedSynchronously = task.IsCompleted)
            {
                callback?.Invoke(this);
            }
            else if (!(callback is null))
            {
                this.callback = callback;
                task.ConfigureAwait(false).GetAwaiter().OnCompleted(OnCompleted);
            }
        }

        private void OnCompleted() => callback?.Invoke(this);

        object? IAsyncResult.AsyncState => state;

        bool IAsyncResult.CompletedSynchronously => completedSynchronously;

        bool IAsyncResult.IsCompleted => task.IsCompleted;

        WaitHandle IAsyncResult.AsyncWaitHandle => ((IAsyncResult)task).AsyncWaitHandle;

        internal void End() => task.ConfigureAwait(false).GetAwaiter().GetResult();

        internal TResult End<TResult>() => ((Task<TResult>)task).ConfigureAwait(false).GetAwaiter().GetResult();
    }
}