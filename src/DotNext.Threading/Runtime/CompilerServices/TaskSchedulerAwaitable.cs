using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

/// <summary>
/// Represents an awaitable object that switches the asynchronous method to the specified task scheduler.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct TaskSchedulerAwaitable
{
    private readonly TaskScheduler scheduler;
    private readonly bool forceYield;

    internal TaskSchedulerAwaitable(TaskScheduler scheduler, bool forceYield)
    {
        this.scheduler = scheduler;
        this.forceYield = forceYield;
    }

    /// <summary>
    /// Gets the awaiter of this object.
    /// </summary>
    /// <returns>The awaiter of this object.</returns>
    public Awaiter GetAwaiter() => new(scheduler, forceYield);

    /// <summary>
    /// Represents the awaiter that switches the asynchronous method to the specified task scheduler.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Awaiter : ICriticalNotifyCompletion
    {
        private readonly TaskScheduler? scheduler;
        private readonly bool forceYield;

        internal Awaiter(TaskScheduler scheduler, bool forceYield)
        {
            this.scheduler = scheduler;
            this.forceYield = forceYield;
        }

        private TaskScheduler Scheduler => scheduler ?? TaskScheduler.Default;

        /// <summary>
        /// Gets a value indicating that the caller is already running on the target scheduler,
        /// so the switch is not needed.
        /// </summary>
        public bool IsCompleted => !forceYield && IsCurrent(Scheduler);

        /// <summary>
        /// Ends the switch.
        /// </summary>
        public void GetResult()
        {
        }

        /// <inheritdoc/>
        public void OnCompleted(Action continuation) => Schedule(continuation, flowExecutionContext: true);

        /// <inheritdoc/>
        public void UnsafeOnCompleted(Action continuation) => Schedule(continuation, flowExecutionContext: false);

        private static bool IsCurrent(TaskScheduler scheduler)
            => SynchronizationContext.Current is null
               && ReferenceEquals(TaskScheduler.Current, scheduler)
               && (!ReferenceEquals(scheduler, TaskScheduler.Default) || Thread.CurrentThread.IsThreadPoolThread);

        private void Schedule(Action continuation, bool flowExecutionContext)
        {
            var schedulerCopy = Scheduler;
            if (ReferenceEquals(schedulerCopy, TaskScheduler.Default))
            {
                // no need to allocate a task: TaskScheduler.Current is Default outside any task
                if (flowExecutionContext)
                {
                    ThreadPool.QueueUserWorkItem(Invoke, continuation, preferLocal: false);
                }
                else
                {
                    ThreadPool.UnsafeQueueUserWorkItem(Invoke, continuation, preferLocal: false);
                }
            }
            else if (flowExecutionContext || ExecutionContext.IsFlowSuppressed())
            {
                StartNew(continuation, schedulerCopy);
            }
            else
            {
                using (ExecutionContext.SuppressFlow())
                {
                    StartNew(continuation, schedulerCopy);
                }
            }

            static void Invoke(Action continuation) => continuation();

            // The continuation must run inside a task executed by the scheduler, so subsequent awaits capture it.
            // HideScheduler must not be used, otherwise TaskScheduler.Current returns Default.
            static void StartNew(Action continuation, TaskScheduler scheduler)
                => Task.Factory.StartNew(continuation, CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler);
        }
    }
}
