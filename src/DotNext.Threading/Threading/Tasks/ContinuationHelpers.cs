using System.Diagnostics;

namespace DotNext.Threading.Tasks;

internal static class ContinuationHelpers
{
    public static object? CaptureSchedulingContext()
    {
        object? schedulingContext = SynchronizationContext.Current;
        if (schedulingContext is null || schedulingContext.GetType() == typeof(SynchronizationContext))
        {
            schedulingContext = TaskScheduler.Current;
            if (ReferenceEquals(schedulingContext, TaskScheduler.Default))
                schedulingContext = null;
        }

        return schedulingContext;
    }

    extension(Action<object?> continuation)
    {
        public void InvokeInExecutionContext(object? state, object schedulingContext,
            ExecutionContext? context)
        {
            if (context is not null)
            {
                var currentContext = ExecutionContext.Capture();
                ExecutionContext.Restore(context);

                try
                {
                    continuation.InvokeInCurrentExecutionContext(state, schedulingContext);
                }
                finally
                {
                    if (currentContext is not null)
                        ExecutionContext.Restore(currentContext);
                }
            }
            else
            {
                continuation.InvokeInCurrentExecutionContext(state, schedulingContext);
            }
        }

        public void InvokeInCurrentExecutionContext(object? state, object schedulingContext)
        {
            switch (schedulingContext)
            {
                case SynchronizationContext context:
                    context.Post(continuation.Invoke, state);
                    break;
                case TaskScheduler scheduler:
                    Task.Factory.StartNew(continuation, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler);
                    break;
                default:
                    Debug.Fail($"Unexpected scheduling context {schedulingContext}");
                    break;
            }
        }
    }
}