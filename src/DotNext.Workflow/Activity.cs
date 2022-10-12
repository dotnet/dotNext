using System.Reflection;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Workflow;

using static Metadata.ActivityMetaModel;

/// <summary>
/// Represents root class for all activities.
/// </summary>
public abstract class Activity
{
    private protected Activity()
    {
    }

    /// <summary>
    /// Gets name of the activity.
    /// </summary>
    /// <typeparam name="TActivity"></typeparam>
    /// <returns></returns>
    public static string GetName<TActivity>()
        where TActivity : Activity
        => GetActivityName(typeof(TActivity));

    /// <summary>
    /// Instructs underlying workflow engine that the workflow reaches the checkpoint and the state
    /// of the workflow can be persisted.
    /// </summary>
    /// <returns>The asynchronous result of the operation.</returns>
    protected static CheckpointResult Checkpoint() => new();
}

/// <summary>
/// Represents a class for custom activities to derive from.
/// </summary>
/// <typeparam name="TInput">The type encapsulating input parameters of the activity; or <see cref="Missing"/> for activity without parameters.</typeparam>
public abstract class Activity<TInput> : Activity
    where TInput : class
{
    /// <summary>
    /// Initializes this instance of activity, asynchronously.
    /// </summary>
    /// <remarks>
    /// This method is called in the beginning of the lifecycle of this instance. However, during the lifetime
    /// of the activity it can be called multiple times (if activity continues its execution on another machine).
    /// </remarks>
    /// <param name="context">The activity execution context.</param>
    /// <returns>The task representing asynchronous result.</returns>
    internal protected virtual ValueTask InitializeAsync(ActivityContext<TInput> context) => ValueTask.CompletedTask;

    internal protected virtual ValueTask CleanupAsync(ActivityContext<TInput> context) => ValueTask.CompletedTask;

    /// <summary>
    /// Provides activity logic.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <returns>Activity result.</returns>
    internal protected abstract ActivityResult ExecuteAsync(ActivityContext<TInput> context);
}