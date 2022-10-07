using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Workflow;

/// <summary>
/// Represents root class for all activities.
/// </summary>
public abstract class Activity
{
    private protected Activity()
    {
    }

    internal static string GetName(Type activityType)
    {
        var result = activityType.Name;
        var index = result.LastIndexOf(nameof(Activity));
        return index > 0 ? result.Remove(index) : result;
    }

    /// <summary>
    /// Gets name of the activity.
    /// </summary>
    /// <typeparam name="TActivity"></typeparam>
    /// <returns></returns>
    public static string GetName<TActivity>()
        where TActivity : Activity
        => GetName(typeof(TActivity));

    internal abstract Type? GetStateMachineType();
}

/// <summary>
/// Represents a class for custom activities to derive from.
/// </summary>
/// <typeparam name="TInput">The type containing input parameters for the activity; or <see cref="Missing"/> for activity without parameters.</typeparam>
public abstract class Activity<TInput> : Activity
    where TInput : class
{
    /// <summary>
    /// Provides activity logic.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="token">The token that can be used to cancel the activity.</param>
    /// <returns>Activity result.</returns>
    protected abstract ActivityResult ExecuteAsync(ActivityContext<TInput> context, CancellationToken token);

    internal sealed override Type? GetStateMachineType()
        => ((Delegate)ExecuteAsync).Method.GetCustomAttribute<StateMachineAttribute>()?.StateMachineType;
}