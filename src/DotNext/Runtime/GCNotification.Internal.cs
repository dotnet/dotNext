using System.Runtime.CompilerServices;
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

    private abstract class GCCallback<T>
    {
        private readonly Action<T, GCMemoryInfo> callback;
        private readonly T state;
        internal GCMemoryInfo MemoryInfo;

        private protected GCCallback(Action<T, GCMemoryInfo> callback, T state)
        {
            this.callback = callback;
            this.state = state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected void Execute() => callback(state, MemoryInfo);

        /// <summary>
        /// Enqueues the callback for asynchronous execution.
        /// </summary>
        internal abstract void Enqueue();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private protected static void UnsafeExecute(object? state)
        {
            Debug.Assert(state is GCCallback<T>);

            Unsafe.As<GCCallback<T>>(state).Execute();
        }
    }

    private sealed class UnsafeCallback<T> : GCCallback<T>, IThreadPoolWorkItem
    {
        internal UnsafeCallback(Action<T, GCMemoryInfo> callback, T state)
            : base(callback, state)
        {
        }

        void IThreadPoolWorkItem.Execute() => Execute();

        internal override void Enqueue()
            => ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: false);
    }

    private sealed class SynchronizationContextBoundCallback<T> : GCCallback<T>
    {
        private readonly SynchronizationContext context;

        internal SynchronizationContextBoundCallback(Action<T, GCMemoryInfo> callback, T state, SynchronizationContext context)
            : base(callback, state)
        {
            Debug.Assert(context is not null);

            this.context = context;
        }

        internal override void Enqueue()
            => context.Post(UnsafeExecute, this);
    }

    private sealed class TaskSchedulerBoundCallback<T> : GCCallback<T>
    {
        private readonly Task task;
        private readonly TaskScheduler scheduler;

        internal TaskSchedulerBoundCallback(Action<T, GCMemoryInfo> callback, T state, TaskScheduler scheduler)
            : base(callback, state)
        {
            Debug.Assert(scheduler is not null);

            this.scheduler = scheduler;
            task = CreateCallbackInvocationTask(this);
        }

        internal static Task CreateCallbackInvocationTask(GCCallback<T> callback)
            => new(UnsafeExecute, callback, CancellationToken.None, TaskCreationOptions.DenyChildAttach);

        internal override void Enqueue() => task.Start(scheduler);
    }

    private abstract class ExecutionContextBoundCallback<T> : GCCallback<T>
    {
        private readonly ExecutionContext context;

        private protected ExecutionContextBoundCallback(Action<T, GCMemoryInfo> callback, T state, ExecutionContext context)
            : base(callback, state)
        {
            Debug.Assert(context is not null);

            this.context = context;
        }

        private protected abstract ContextCallback Callback { get; }

        internal sealed override void Enqueue()
            => ExecutionContext.Run(context, Callback, this);
    }

    private sealed class SafeCallback<T> : ExecutionContextBoundCallback<T>
    {
        internal SafeCallback(Action<T, GCMemoryInfo> callback, T state, ExecutionContext context)
            : base(callback, state, context)
        {
        }

        private protected override ContextCallback Callback
        {
            get
            {
                return QueueCallback;

                static void QueueCallback(object? callback)
                    => ThreadPool.QueueUserWorkItem(UnsafeExecute, callback, preferLocal: false);
            }
        }
    }

    private sealed class ExecutionAndSynchronizationContextBoundCallback<T> : ExecutionContextBoundCallback<T>
    {
        private readonly SynchronizationContext context;

        internal ExecutionAndSynchronizationContextBoundCallback(Action<T, GCMemoryInfo> callback, T state, ExecutionContext context, SynchronizationContext syncContext)
            : base(callback, state, context)
        {
            Debug.Assert(syncContext is not null);

            this.context = syncContext;
        }

        private protected override ContextCallback Callback
        {
            get
            {
                return QueueCallback;

                static void QueueCallback(object? callback)
                {
                    Debug.Assert(callback is ExecutionAndSynchronizationContextBoundCallback<T>);

                    Unsafe.As<ExecutionAndSynchronizationContextBoundCallback<T>>(callback).Post();
                }
            }
        }

        private void Post() => context.Post(UnsafeExecute, this);
    }

    private sealed class ExecutionContextAndTaskSchedulerBoundCallback<T> : ExecutionContextBoundCallback<T>
    {
        private readonly TaskScheduler scheduler;
        private readonly Task task;

        internal ExecutionContextAndTaskSchedulerBoundCallback(Action<T, GCMemoryInfo> callback, T state, ExecutionContext context, TaskScheduler scheduler)
            : base(callback, state, context)
        {
            Debug.Assert(scheduler is not null);

            this.scheduler = scheduler;
            task = TaskSchedulerBoundCallback<T>.CreateCallbackInvocationTask(this);
        }

        private protected override ContextCallback Callback
        {
            get
            {
                return QueueCallback;

                static void QueueCallback(object? callback)
                {
                    Debug.Assert(callback is ExecutionContextAndTaskSchedulerBoundCallback<T>);

                    Unsafe.As<ExecutionContextAndTaskSchedulerBoundCallback<T>>(callback).Start();
                }
            }
        }

        private void Start() => task.Start(scheduler);
    }

    private sealed class Tracker<T> : IGCCallback
    {
        private readonly GCNotification filter;
        private readonly GCCallback<T> callback;

        internal Tracker(GCNotification filter, T state, Action<T, GCMemoryInfo> callback, bool continueOnCapturedContext)
        {
            Debug.Assert(filter is not null);
            Debug.Assert(callback is not null);

            this.filter = filter;

            // preallocate everything we can to prevent memory allocations in Finalizer
            var executionContext = ExecutionContext.Capture();
            if (!continueOnCapturedContext)
            {
                this.callback = executionContext is null
                    ? new UnsafeCallback<T>(callback, state)
                    : new SafeCallback<T>(callback, state, executionContext);
            }
            else if (SynchronizationContext.Current is SynchronizationContext syncContext && syncContext.GetType() != typeof(SynchronizationContext))
            {
                this.callback = executionContext is null
                    ? new SynchronizationContextBoundCallback<T>(callback, state, syncContext)
                    : new ExecutionAndSynchronizationContextBoundCallback<T>(callback, state, executionContext, syncContext);
            }
            else
            {
                this.callback = executionContext is null
                    ? new TaskSchedulerBoundCallback<T>(callback, state, TaskScheduler.Current)
                    : new ExecutionContextAndTaskSchedulerBoundCallback<T>(callback, state, executionContext, TaskScheduler.Current);
            }
        }

        ~Tracker()
        {
            var memoryInfo = GC.GetGCMemoryInfo();
            if (filter.Test(in memoryInfo))
            {
                callback.MemoryInfo = memoryInfo;
                callback.Enqueue();
            }
            else
            {
                GC.ReRegisterForFinalize(this);
            }
        }
    }
}