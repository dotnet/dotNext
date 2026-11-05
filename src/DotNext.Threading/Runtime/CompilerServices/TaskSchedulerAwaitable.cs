using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

using Threading.Tasks;

/// <summary>
/// Represents an awaitable object that switches the asynchronous method to the specified task scheduler.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct TaskSchedulerAwaitable
{
    private readonly TaskScheduler scheduler;

    internal TaskSchedulerAwaitable(TaskScheduler scheduler)
        => this.scheduler = scheduler;
    
    internal bool IsYieldForced { get; init; }

    /// <summary>
    /// Gets the awaiter of this object.
    /// </summary>
    /// <returns>The awaiter of this object.</returns>
    public Awaiter GetAwaiter() => new(scheduler) { IsYieldForced = IsYieldForced };

    /// <summary>
    /// Represents the awaiter that switches the asynchronous method to the specified task scheduler.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Awaiter : ICriticalNotifyCompletion
    {
        private readonly TaskScheduler? scheduler;

        internal Awaiter(TaskScheduler scheduler)
            => this.scheduler = scheduler;

        internal bool IsYieldForced
        {
            get;
            init;
        }

        private TaskScheduler Scheduler => scheduler ?? TaskScheduler.Default;

        /// <summary>
        /// Gets a value indicating that the caller is already running on the target scheduler,
        /// so the switch is not needed.
        /// </summary>
        public bool IsCompleted => !IsYieldForced && Scheduler.IsCurrent;

        /// <summary>
        /// Ends the switch.
        /// </summary>
        public void GetResult()
        {
        }

        /// <inheritdoc/>
        public void OnCompleted(Action continuation)
            => Scheduler.Schedule(continuation, flowExecutionContext: true);

        /// <inheritdoc/>
        public void UnsafeOnCompleted(Action continuation)
            => Scheduler.Schedule(continuation, flowExecutionContext: false);
    }
}

file static class TaskSchedulerHelpers
{
    extension(TaskScheduler scheduler)
    {
        public bool IsCurrent => SynchronizationContext.Current is null
                                 && ReferenceEquals(TaskScheduler.Current, scheduler)
                                 && (!ReferenceEquals(scheduler, TaskScheduler.Default) || Thread.CurrentThread.IsThreadPoolThread);
        
        public void Schedule(Action continuation, bool flowExecutionContext)
        {
            if (ReferenceEquals(scheduler, TaskScheduler.Default))
            {
                // no need to allocate a task: TaskScheduler.Current is Default outside any task
                continuation.Schedule(flowExecutionContext);
            }
            else if (flowExecutionContext || ExecutionContext.IsFlowSuppressed())
            {
                continuation.Schedule(scheduler);
            }
            else
            {
                using (ExecutionContext.SuppressFlow())
                {
                    continuation.Schedule(scheduler);
                }
            }
        }
    }
}