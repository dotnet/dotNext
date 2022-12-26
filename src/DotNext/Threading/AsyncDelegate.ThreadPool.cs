using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading;

public static partial class AsyncDelegate
{
    private static void QueueUserWorkItem(IThreadPoolWorkItem workItem, bool preferLocal)
    {
        Debug.Assert(workItem is not null);

        var scheduler = TaskScheduler.Current;

        if (ReferenceEquals(scheduler, TaskScheduler.Default))
        {
            ThreadPool.UnsafeQueueUserWorkItem(workItem, preferLocal);
        }
        else
        {
            Task.Factory.StartNew(
                static workItem =>
                {
                    Debug.Assert(workItem is IThreadPoolWorkItem);
                    Unsafe.As<IThreadPoolWorkItem>(workItem).Execute();
                },
                workItem);
        }
    }

    /// <summary>
    /// Enqueues invocation of the delegate to the thread pool.
    /// </summary>
    /// <remarks>
    /// This method is alternative form of <see cref="Task.Run(Func{Task?}, CancellationToken)"/> that consumes
    /// less memory.
    /// </remarks>
    /// <typeparam name="TArgs">The type of the container with arguments to be passed to supplied function.</typeparam>
    /// <typeparam name="TResult">The result of invocation.</typeparam>
    /// <param name="function">The function to be invoked.</param>
    /// <param name="args">The arguments to be passed to <paramref name="function"/>.</param>
    /// <param name="runContinuationsAsynchronously"><see langword="true"/> to execute continuations attached to the returned task asynchronously; otherwise, <see langword="false"/>.</param>
    /// <param name="continueOnCapturedContext"><see langword="true"/> to execute <paramref name="function"/> in the current context; otherwise, <see langword="false"/>.</param>
    /// <param name="state">An object to be provided from <see cref="Task.AsyncState"/> property of the returned task.</param>
    /// <param name="preferLocal">
    /// <see langword="true"/> to prefer queueing the work item in a queue close to the current thread;
    /// <see langword="false"/> to prefer queueing the work item to the thread pool's shared queue.
    /// </param>
    /// <param name="token">The token to be passed to <paramref name="function"/>.</param>
    /// <returns>The task representing asynchronous execution of <paramref name="function"/>.</returns>
    public static Task<TResult> EnqueueToThreadPool<TArgs, TResult>(this RefFunc<TArgs, CancellationToken, ValueTask<TResult>> function, in TArgs args, bool runContinuationsAsynchronously = true, bool continueOnCapturedContext = true, object? state = null, bool preferLocal = false, CancellationToken token = default)
        where TArgs : struct
    {
        if (function is null)
            return Task.FromException<TResult>(new ArgumentNullException(nameof(function)));

        var workItem = !continueOnCapturedContext || ExecutionContext.Capture() is not ExecutionContext context
            ? new AsyncWorkItem<TArgs, TResult>(args, function, runContinuationsAsynchronously, state) { Token = token }
            : new AsyncContextfulWorkItem<TArgs, TResult>(args, function, runContinuationsAsynchronously, state, context) { Token = token };

        QueueUserWorkItem(workItem, preferLocal);
        return workItem.Task;
    }

    private class AsyncWorkItem<TState, TResult> : TaskCompletionSource<TResult>, IThreadPoolWorkItem
        where TState : struct
    {
        private RefFunc<TState, CancellationToken, ValueTask<TResult>>? callback;
        private TState args;
        private CancellationToken token;
        private ConfiguredValueTaskAwaitable<TResult>.ConfiguredValueTaskAwaiter awaiter;

        internal AsyncWorkItem(in TState args, RefFunc<TState, CancellationToken, ValueTask<TResult>> callback, bool runContinuationsAsynchronously, object? state)
            : base(state, runContinuationsAsynchronously ? TaskCreationOptions.RunContinuationsAsynchronously : TaskCreationOptions.None)
        {
            Debug.Assert(callback is not null);

            this.args = args;
            this.callback = callback;
        }

        internal CancellationToken Token
        {
            init => token = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected void Execute(bool continueOnCapturedContext)
        {
            var args = this.args;
            var callback = this.callback;
            var token = this.token;

            Debug.Assert(callback is not null);

            // help GC
            this.callback = null;
            this.args = default;
            this.token = default;

            var awaiter = callback.Invoke(ref args, token).ConfigureAwait(false).GetAwaiter();
            if (awaiter.IsCompleted)
            {
                Complete(ref awaiter);
            }
            else
            {
                this.awaiter = awaiter;
                Action continuation = OnCompleted;

                if (continueOnCapturedContext)
                    awaiter.OnCompleted(continuation);
                else
                    awaiter.UnsafeOnCompleted(continuation);
            }
        }

        void IThreadPoolWorkItem.Execute() => Execute(continueOnCapturedContext: false);

        private void OnCompleted() => Complete(ref awaiter);

        private void Complete(ref ConfiguredValueTaskAwaitable<TResult>.ConfiguredValueTaskAwaiter awaiter)
        {
            try
            {
                SetResult(awaiter.GetResult());
            }
            catch (OperationCanceledException e)
            {
                SetCanceled(e.CancellationToken);
            }
            catch (Exception e)
            {
                SetException(e);
            }
            finally
            {
                awaiter = default; // help GC
            }
        }
    }

    private sealed class AsyncContextfulWorkItem<TState, TResult> : AsyncWorkItem<TState, TResult>, IThreadPoolWorkItem
        where TState : struct
    {
        private readonly ExecutionContext context;

        internal AsyncContextfulWorkItem(in TState state, RefFunc<TState, CancellationToken, ValueTask<TResult>> callback, bool runContinuationsAsynchronously, object? asyncState, ExecutionContext context)
            : base(state, callback, runContinuationsAsynchronously, asyncState)
        {
            Debug.Assert(context is not null);

            this.context = context;
        }

        void IThreadPoolWorkItem.Execute()
        {
            ExecutionContext.Run(
                context,
                static state =>
                {
                    Debug.Assert(state is AsyncContextfulWorkItem<TState, TResult>);

                    Unsafe.As<AsyncContextfulWorkItem<TState, TResult>>(state).Execute(continueOnCapturedContext: true);
                },
                this);
        }
    }
}