using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Workflow;

using ExceptionAggregator = Runtime.ExceptionServices.ExceptionAggregator;
using Timeout = Threading.Timeout;

/// <summary>
/// Represents activity execution context.
/// </summary>
public abstract class ActivityContext
{
    private readonly Timeout remainingTime;

    // null, or Func<Task>, or Action, or List<Delegate>
    private object? checkpointCallbacks;

    private protected ActivityContext(string instanceName, TimeSpan timeout)
    {
        Debug.Assert(instanceName is { Length: > 0 });

        InstanceName = instanceName;
        remainingTime = new(timeout);
    }

    /// <summary>
    /// Gets the unique name of executing activity.
    /// </summary>
    public string InstanceName { get; }

    /// <summary>
    /// Gets a value indicating that the activity is canceled by the request.
    /// </summary>
    public bool IsCanceled { get; internal init; }

    private void OnCheckpoint(Delegate callback)
    {
        switch (checkpointCallbacks)
        {
            case null:
                checkpointCallbacks = callback;
                break;
            case Action action:
                checkpointCallbacks = new List<Delegate> { action };
                break;
            case Func<Task> func:
                checkpointCallbacks = new List<Delegate> { func };
                break;
            case List<Delegate> list:
                list.Add(callback);
                break;
        }
    }

    /// <summary>
    /// Executes callback immediately after persistence of the snapshot.
    /// </summary>
    /// <param name="callback">The callback to be called.</param>
    public void OnCheckpoint(Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        OnCheckpoint(callback.As<Delegate>());
    }

    /// <summary>
    /// Executes callback immediately after persistence of the snapshot.
    /// </summary>
    /// <param name="callback">The callback to be called.</param>
    public void OnCheckpoint(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        OnCheckpoint(callback.As<Delegate>());
    }

    internal ValueTask OnCheckpoint()
    {
        ValueTask result;

        switch (checkpointCallbacks)
        {
            case null:
                goto default;
            case Action action:
                result = ValueTask.CompletedTask;
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException(e);
                }

                break;
            case Func<Task> action:
                result = new(action());
                break;
            case List<Delegate> actions:
                result = InvokeAsync(actions);
                break;
            default:
                result = ValueTask.CompletedTask;
                break;
        }

        checkpointCallbacks = null;
        return result;

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask InvokeAsync(List<Delegate> callbacks)
        {
            var exceptions = new ExceptionAggregator();

            foreach (var cb in callbacks)
            {
                try
                {
                    switch (cb)
                    {
                        case Action action:
                            action();
                            break;
                        case Func<Task> func:
                            await func().ConfigureAwait(false);
                            break;
                    }
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            exceptions.ThrowIfNeeded();
        }
    }

    public ref readonly Timeout RemainingTime => ref remainingTime;

    /// <summary>
    /// Gets currently executing activity.
    /// </summary>
    public abstract Activity ExecutingActivity { get; }

    /// <summary>
    /// Gets context of the caller activity.
    /// </summary>
    public ActivityContext? Parent { get; internal init; }
}

public sealed class ActivityContext<TInput> : ActivityContext
    where TInput : class
{
    internal ActivityContext(string instanceName, TimeSpan timeout, Activity<TInput> activity)
        : base(instanceName, timeout)
    {
        Debug.Assert(activity is not null);

        ExecutingActivity = activity;
    }

    public override Activity<TInput> ExecutingActivity { get; }
}