using Debug = System.Diagnostics.Debug;

namespace DotNext.Runtime;

public partial class GCNotification
{
    private sealed class Tracker : TaskCompletionSource<GCMemoryInfo>, IGCCallback
    {
        private readonly GCNotification filter;

        internal Tracker(GCNotification filter)
            : base(TaskCreationOptions.RunContinuationsAsynchronously)
        {
            Debug.Assert(filter is not null);

            this.filter = filter;
        }

        void IGCCallback.StopTracking() => GC.SuppressFinalize(this);

        ~Tracker()
        {
            var memoryInfo = GC.GetGCMemoryInfo();
            if (filter.Test(in memoryInfo))
            {
                TrySetResult(memoryInfo);
            }
            else
            {
                GC.ReRegisterForFinalize(this);
            }
        }
    }

    private sealed class CallbackContext<T> : Tuple<Action<T, GCMemoryInfo>, T, GCMemoryInfo>, IThreadPoolWorkItem
    {
        internal CallbackContext(Action<T, GCMemoryInfo> callback, T state, in GCMemoryInfo info)
            : base(callback, state, info)
        {
        }

        public void Execute() => Item1.Invoke(Item2, Item3);
    }

    private sealed class Tracker<T> : IGCCallback
    {
        private readonly GCNotification filter;
        private readonly T state;
        private readonly Action<T, GCMemoryInfo> callback;
        private readonly object? capturedContext;
        private readonly ExecutionContext? context;

        internal Tracker(GCNotification filter, T state, Action<T, GCMemoryInfo> callback, bool continueOnCapturedContext)
        {
            Debug.Assert(filter is not null);
            Debug.Assert(callback is not null);

            this.filter = filter;
            this.state = state;
            this.callback = callback;

            context = ExecutionContext.Capture();
            if (!continueOnCapturedContext)
            {
                capturedContext = null;
            }
            else if (SynchronizationContext.Current is SynchronizationContext syncContext && syncContext.GetType() != typeof(SynchronizationContext))
            {
                capturedContext = syncContext;
            }
            else
            {
                capturedContext = TaskScheduler.Current;
            }
        }

        void IGCCallback.StopTracking() => GC.SuppressFinalize(this);

        private static void InvokeCallback(object? capturedContext, Action<T, GCMemoryInfo> callback, T state, in GCMemoryInfo info, bool flowExecutionContext)
        {
            var args = new CallbackContext<T>(callback, state, info);
            switch (capturedContext)
            {
                case SynchronizationContext context:
                    context.Post(static state => (state as CallbackContext<T>)?.Execute(), args);
                    break;
                case TaskScheduler scheduler:
                    Task.Factory.StartNew(static state => (state as CallbackContext<T>)?.Execute(), args, CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler);
                    break;
                default:
                    if (flowExecutionContext)
                        ThreadPool.QueueUserWorkItem(static state => state.Execute(), args, preferLocal: false);
                    else
                        ThreadPool.UnsafeQueueUserWorkItem(args, preferLocal: false);

                    break;
            }
        }

        private void InvokeCallback(in GCMemoryInfo info, bool flowExecutionContext)
            => InvokeCallback(capturedContext, callback, state, in info, flowExecutionContext);

        private void InvokeCallback(in GCMemoryInfo info)
        {
            if (context is null)
            {
                InvokeCallback(in info, flowExecutionContext: false);
            }
            else
            {
                ExecutionContext.Run(
                    context,
                    static state =>
                    {
                        if (state is Tuple<Tracker<T>, GCMemoryInfo> tuple)
                            tuple.Item1.InvokeCallback(tuple.Item2, flowExecutionContext: true);
                    },
                    new Tuple<Tracker<T>, GCMemoryInfo>(this, info));
            }
        }

        ~Tracker()
        {
            var memoryInfo = GC.GetGCMemoryInfo();
            if (filter.Test(in memoryInfo))
            {
                InvokeCallback(in memoryInfo);
            }
            else
            {
                GC.ReRegisterForFinalize(this);
            }
        }
    }
}