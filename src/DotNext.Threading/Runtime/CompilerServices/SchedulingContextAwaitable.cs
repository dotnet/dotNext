using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

using Threading.Tasks;

/// <summary>
/// Represents an awaitable object that resumes the asynchronous method
/// on the captured synchronization context or task scheduler.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct SchedulingContextAwaitable
{
    private readonly SchedulingContext schedulingContext;

    internal SchedulingContextAwaitable(SchedulingContext schedulingContext)
        => this.schedulingContext = schedulingContext;

    /// <summary>
    /// Gets the awaiter of this object.
    /// </summary>
    /// <returns>The awaiter of this object.</returns>
    public Awaiter GetAwaiter() => new(schedulingContext);

    /// <summary>
    /// Represents the awaiter that resumes the asynchronous method
    /// on the captured synchronization context or task scheduler.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Awaiter : ICriticalNotifyCompletion
    {
        private readonly SchedulingContext schedulingContext;

        internal Awaiter(SchedulingContext schedulingContext) => this.schedulingContext = schedulingContext;

        private TaskSchedulerAwaitable.Awaiter SchedulerAwaiter
            => new(schedulingContext.Scheduler);

        /// <summary>
        /// Gets a value indicating that the caller is already running in the captured context,
        /// so the switch is not needed.
        /// </summary>
        public bool IsCompleted => schedulingContext.Context is { } context
            ? ReferenceEquals(SynchronizationContext.Current, context) && ReferenceEquals(TaskScheduler.Current, TaskScheduler.Default)
            : SchedulerAwaiter.IsCompleted;

        /// <summary>
        /// Ends the switch.
        /// </summary>
        public void GetResult()
        {
        }

        /// <inheritdoc/>
        public void OnCompleted(Action continuation)
        {
            if (schedulingContext.Context is not { } context)
            {
                SchedulerAwaiter.OnCompleted(continuation);
            }
            else if (ExecutionContext.Capture() is { } executionContext)
            {
                context.Post(static state =>
                {
                    Debug.Assert(state is Tuple<Action, ExecutionContext>);

                    var (continuation, executionContext) = Unsafe.As<Tuple<Action, ExecutionContext>>(state);
                    executionContext.Run(continuation);
                }, Tuple.Create(continuation, executionContext));
            }
            else
            {
                context.Post(continuation);
            }
        }

        /// <inheritdoc/>
        public void UnsafeOnCompleted(Action continuation)
        {
            if (schedulingContext.Context is { } context)
            {
                // posted directly instead of wrapping into a task, so TaskScheduler.Current remains Default
                context.Post(continuation);
            }
            else
            {
                SchedulerAwaiter.UnsafeOnCompleted(continuation);
            }
        }
    }
}
