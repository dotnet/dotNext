using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Tasks;

[StructLayout(LayoutKind.Auto)]
internal readonly struct SchedulingContext
{
    private readonly object? context;

    public SchedulingContext()
    {
        context = SynchronizationContext.Current;
        if (context is null || context.GetType() == typeof(SynchronizationContext))
        {
            context = TaskScheduler.Current;
            if (ReferenceEquals(context, TaskScheduler.Default))
                context = null;
        }
    }

    public TaskScheduler Scheduler => context as TaskScheduler ?? TaskScheduler.Default;

    public SynchronizationContext? Context => context as SynchronizationContext;

    public bool IsCaptured => context is not null;
    
    public void Invoke(Action<object?> action, object? state)
    {
        switch (context)
        {
            case SynchronizationContext syncContext:
                syncContext.Post(action, state);
                break;
            case TaskScheduler scheduler:
                action.Schedule(scheduler, state);
                break;
            default:
                Debug.Fail($"Unexpected scheduling context {context}");
                break;
        }
    }

    public void Invoke(Action<object?> action, object? state, ExecutionContext? executionContext)
    {
        if (executionContext is null)
        {
            Invoke(action, state);
        }
        else
        {
            executionContext.Run<ContextfulAction>(new(action, state, this));
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct ContextfulAction(Action<object?> action, object? state, SchedulingContext context) : IExecutionContextAction
    {
        void IExecutionContextAction.Invoke() => context.Invoke(action, state);

        static bool IExecutionContextAction.MayChangeSynchronizationContext => false;
    }
}