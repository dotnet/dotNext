using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

/// <summary>
/// Represents an awaitable object that resumes the asynchronous method
/// on the captured synchronization context or task scheduler.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct SchedulingContextAwaitable
{
    // SynchronizationContext, TaskScheduler or null for TaskScheduler.Default
    private readonly object? schedulingContext;

    internal SchedulingContextAwaitable(object? schedulingContext) => this.schedulingContext = schedulingContext;

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
        private readonly object? schedulingContext;

        internal Awaiter(object? schedulingContext) => this.schedulingContext = schedulingContext;

        private TaskSchedulerAwaitable.Awaiter SchedulerAwaiter
            => new(schedulingContext as TaskScheduler ?? TaskScheduler.Default);

        /// <summary>
        /// Gets a value indicating that the caller is already running in the captured context,
        /// so the switch is not needed.
        /// </summary>
        public bool IsCompleted => schedulingContext is SynchronizationContext context
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
            if (schedulingContext is not SynchronizationContext context)
            {
                SchedulerAwaiter.OnCompleted(continuation);
            }
            else if (ExecutionContext.Capture() is { } executionContext)
            {
                context.Post(static state =>
                {
                    Debug.Assert(state is Tuple<Action, ExecutionContext>);

                    var (continuation, executionContext) = Unsafe.As<Tuple<Action, ExecutionContext>>(state);
                    ExecutionContext.Run(executionContext, Invoke, continuation);
                }, Tuple.Create(continuation, executionContext));
            }
            else
            {
                context.Post(Invoke, continuation);
            }
        }

        /// <inheritdoc/>
        public void UnsafeOnCompleted(Action continuation)
        {
            if (schedulingContext is SynchronizationContext context)
            {
                // posted directly instead of wrapping into a task, so TaskScheduler.Current remains Default
                context.Post(Invoke, continuation);
            }
            else
            {
                SchedulerAwaiter.UnsafeOnCompleted(continuation);
            }
        }

        private static void Invoke(object? continuation)
        {
            Debug.Assert(continuation is Action);

            Unsafe.As<Action>(continuation).Invoke();
        }
    }
}
