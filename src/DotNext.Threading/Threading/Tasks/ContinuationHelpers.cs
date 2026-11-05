using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Tasks;

internal static class ContinuationHelpers
{
    extension(Action action)
    {
        public void Schedule(bool flowExecutionContext, bool preferLocal = false)
            => Schedule(Invoke, action, flowExecutionContext, preferLocal);
        
        // The continuation must run inside a task executed by the scheduler, so subsequent awaits capture it.
        // HideScheduler must not be used, otherwise TaskScheduler.Current returns Default.
        public void Schedule(TaskScheduler scheduler)
            => Schedule(Invoke, scheduler, action);
    }

    extension(Action<object?> action)
    {
        public void Schedule(object? state, bool flowExecutionContext, bool preferLocal = false)
        {
            if (flowExecutionContext)
            {
                ThreadPool.QueueUserWorkItem(action, state, preferLocal);
            }
            else
            {
                ThreadPool.UnsafeQueueUserWorkItem(action, state, preferLocal);
            }
        }
        
        public void Schedule(TaskScheduler scheduler, object? state)
            => Task.Factory.StartNew(action, state, CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler);
    }

    extension(SynchronizationContext context)
    {
        public void Post(Action callback)
            => context.Post(Invoke, callback);

        public void Post(Action callback, ExecutionContext executionContext)
        {
            context.Post(Execute, Tuple.Create(callback, executionContext));
            
            static void Execute(object? state)
            {
                Debug.Assert(state is Tuple<Action, ExecutionContext>);

                var (continuation, executionContext) = Unsafe.As<Tuple<Action, ExecutionContext>>(state);
                executionContext.Run(continuation);
            }
        }

        public void Post(Action<object?> callback, object? state)
            => context.Post(callback.Invoke, state);
    }

    extension(ExecutionContext context)
    {
        private void Run(Action callback)
            => Run<ExecutionContextAction>(context, new(callback));

        public void Run<T>(Action<T> callback, T arg)
            where T : allows ref struct
            => Run<ExecutionContextAction<T>>(context, new(callback, arg));

        public void Run<TAction>(TAction action)
            where TAction : IExecutionContextAction, allows ref struct
        {
            // Capture returns null if the flow is suppressed, so the flow must be restored temporarily
            var flowSuppressed = ExecutionContext.IsFlowSuppressed();
            if (flowSuppressed)
                ExecutionContext.RestoreFlow();

            var currentContext = ExecutionContext.Capture();
            Debug.Assert(currentContext is not null);
            ExecutionContext.Restore(context);

            // Arbitrary callbacks can change the synchronization context, so it must be restored
            // as ExecutionContext.Run does
            var syncContext = TAction.MayChangeSynchronizationContext
                ? SynchronizationContext.Current
                : null;
            try
            {
                action.Invoke();
            }
            finally
            {
                if (TAction.MayChangeSynchronizationContext
                    && !ReferenceEquals(SynchronizationContext.Current, syncContext))
                    SynchronizationContext.SetSynchronizationContext(syncContext);
                
                ExecutionContext.Restore(currentContext);

                // AsyncFlowControl of the caller remains valid: Undo checks the thread and the suppression only
                if (flowSuppressed)
                    ExecutionContext.SuppressFlow();
            }
        }
    }
    
    private static void Invoke(object? continuation)
    {
        Debug.Assert(continuation is Action);

        Unsafe.As<Action>(continuation).Invoke();
    }
}

[StructLayout(LayoutKind.Auto)]
file readonly ref struct ExecutionContextAction(Action action) : IExecutionContextAction
{
    void IExecutionContextAction.Invoke() => action();
}

[StructLayout(LayoutKind.Auto)]
file readonly ref struct ExecutionContextAction<T>(Action<T> action, T arg) : IExecutionContextAction
    where T : allows ref struct
{
    private readonly T arg = arg;

    void IExecutionContextAction.Invoke() => action(arg);
}