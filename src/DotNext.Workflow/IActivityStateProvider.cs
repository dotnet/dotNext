using IAsyncStateMachine = System.Runtime.CompilerServices.IAsyncStateMachine;

namespace DotNext.Workflow;

/// <summary>
/// Provides activity state stored externally.
/// </summary>
public interface IActivityStateProvider
{
    /// <summary>
    /// Gets runtime state of the activity.
    /// </summary>
    /// <typeparam name="TInput">The type encapsulating input parameters of the activity.</typeparam>
    /// <typeparam name="TState">The execution state of the activity.</typeparam>
    /// <returns>The input data and execution state of the activity.</returns>
    (TInput, TState) GetRuntimeState<TInput, TState>()
        where TInput : class
        where TState : IAsyncStateMachine;

    /// <summary>
    /// Gets workflow deadline.
    /// </summary>
    TimeSpan RemainingTime { get; }

    /// <summary>
    /// Gets workflow instance name.
    /// </summary>
    string InstanceName { get; }
}

internal abstract class InitialActivityStateProvider : IActivityStateProvider
{
    public abstract (TInput, TState) GetRuntimeState<TInput, TState>()
        where TInput : class
        where TState : IAsyncStateMachine;

    public abstract TimeSpan RemainingTime { get; }

    public abstract string InstanceName { get; }

    internal abstract ValueTask InvokeCallback(IActivityStartedCallback callback, ActivityInstance instance);
}