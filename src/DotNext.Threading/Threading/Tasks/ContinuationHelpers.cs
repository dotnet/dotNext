using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Tasks;

internal static class ContinuationHelpers
{
    extension(Action action)
    {
        public void Schedule(bool flowExecutionContext, bool preferLocal = false)
        {
            var workItem = Invoke;
            if (flowExecutionContext)
            {
                ThreadPool.QueueUserWorkItem(workItem, action, preferLocal);
            }
            else
            {
                ThreadPool.UnsafeQueueUserWorkItem(workItem, action, preferLocal);
            }
        }
        
        // The continuation must run inside a task executed by the scheduler, so subsequent awaits capture it.
        // HideScheduler must not be used, otherwise TaskScheduler.Current returns Default.
        public void Schedule(TaskScheduler scheduler)
            => Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.DenyChildAttach, scheduler);
    }

    extension(SynchronizationContext context)
    {
        public void Post(Action callback)
            => context.Post(Invoke, callback);
    }

    extension(ExecutionContext context)
    {
        public void Run(Action callback)
            => Run<ActionWorkItem>(context, new(callback));

        public void Run<T>(Action<T> callback, T arg)
            where T : allows ref struct
            => Run<ActionWorkItem<T>>(context, new(callback, arg));

        public void Run<TWorkItem>(TWorkItem workItem)
            where TWorkItem : IThreadPoolWorkItem, allows ref struct
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
                workItem.Execute();
            }
            finally
            {
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

// Arbitrary callbacks can change the synchronization context, so it must be restored
// as ExecutionContext.Run does
[StructLayout(LayoutKind.Auto)]
file readonly ref struct ActionWorkItem(Action action) : IThreadPoolWorkItem
{
    void IThreadPoolWorkItem.Execute()
    {
        var context = SynchronizationContext.Current;
        try
        {
            action();
        }
        finally
        {
            context.RestoreIfChanged();
        }
    }
}

[StructLayout(LayoutKind.Auto)]
file readonly ref struct ActionWorkItem<T>(Action<T> action, T arg) : IThreadPoolWorkItem
    where T : allows ref struct
{
    private readonly T arg = arg;

    void IThreadPoolWorkItem.Execute()
    {
        var context = SynchronizationContext.Current;
        try
        {
            action(arg);
        }
        finally
        {
            context.RestoreIfChanged();
        }
    }
}

file static class SynchronizationContextHelpers
{
    public static void RestoreIfChanged(this SynchronizationContext? context)
    {
        if (!ReferenceEquals(SynchronizationContext.Current, context))
            SynchronizationContext.SetSynchronizationContext(context);
    }
}