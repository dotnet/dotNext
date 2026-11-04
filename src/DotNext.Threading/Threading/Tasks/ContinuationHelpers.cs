using System.Diagnostics;
using System.Runtime.CompilerServices;

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void InvokeInExecutionContext(object? state, object schedulingContext,
            ExecutionContext? context)
        {
            if (context is not null)
            {
                continuation.InvokeInExecutionContextCore(state, schedulingContext, context);
            }
            else
            {
                continuation.InvokeInCurrentExecutionContext(state, schedulingContext);
            }
        }
        
        private void InvokeInExecutionContextCore(object? state, object schedulingContext, ExecutionContext context)
        {
            // Capture returns null if the flow is suppressed, so the flow must be restored temporarily
            var flowSuppressed = ExecutionContext.IsFlowSuppressed();
            if (flowSuppressed)
                ExecutionContext.RestoreFlow();

            var currentContext = ExecutionContext.Capture();
            Debug.Assert(currentContext is not null);
            ExecutionContext.Restore(context);

            try
            {
                continuation.InvokeInCurrentExecutionContext(state, schedulingContext);
            }
            finally
            {
                ExecutionContext.Restore(currentContext);

                // AsyncFlowControl of the caller remains valid: Undo checks the thread and the suppression only
                if (flowSuppressed)
                    ExecutionContext.SuppressFlow();
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