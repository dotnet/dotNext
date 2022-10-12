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
    private readonly CancellationToken token;
    private readonly ActivityInstance instance;

    // null, or Func<Task>, or Action, or List<Delegate>
    private object? checkpointCallbacks;
    private readonly Task? activityTask;

    private protected ActivityContext(WorkflowEngine engine, in ActivityInstance instance, TimeSpan timeout, CancellationToken token)
    {
        Debug.Assert(engine is not null);

        Engine = engine;
        this.instance = instance;
        remainingTime = new(timeout);
        this.token = token;
    }

    internal Task ActivityTask
    {
        get => activityTask ?? Task.CompletedTask;
        init => activityTask = value;
    }

    /// <summary>
    /// Gets the unique name of executing activity.
    /// </summary>
    public ref readonly ActivityInstance Instance => ref instance;

    /// <summary>
    /// Gets cancellation token.
    /// </summary>
    public ref readonly CancellationToken Token => ref token;

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

    /// <summary>
    /// Gets deadline of the activity.
    /// </summary>
    public ref readonly Timeout RemainingTime => ref remainingTime;

    /// <summary>
    /// Gets currently executing activity.
    /// </summary>
    public abstract Activity ExecutingActivity { get; }

    /// <summary>
    /// Gets input data.
    /// </summary>
    public abstract object Input { get; }

    /// <summary>
    /// Gets workflow engine.
    /// </summary>
    public WorkflowEngine Engine { get; }

    internal abstract ValueTask InitializeAsync();

    internal abstract ValueTask CleanupAsync();
}

public sealed class ActivityContext<TInput> : ActivityContext
    where TInput : class
{
    internal ActivityContext(TInput input, Activity<TInput> activity, WorkflowEngine engine, in ActivityInstance instance, TimeSpan timeout, CancellationToken token)
        : base(engine, in instance, timeout, token)
    {
        Debug.Assert(activity is not null);
        Debug.Assert(input is not null);

        ExecutingActivity = activity;
        Input = input;
    }

    /// <inheritdoc />
    public override Activity<TInput> ExecutingActivity { get; }

    /// <inheritdoc />
    public override TInput Input { get; }

    internal override ValueTask InitializeAsync() => ExecutingActivity.InitializeAsync(this);

    internal override ValueTask CleanupAsync() => ExecutingActivity.CleanupAsync(this);
}