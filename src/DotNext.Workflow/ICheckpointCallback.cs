using IAsyncStateMachine = System.Runtime.CompilerServices.IAsyncStateMachine;

namespace DotNext.Workflow;

internal interface ICheckpointCallback
{
    Task CheckpointReachedAsync<TState>(ActivityInstance instance, TState executionState, TimeSpan remainingTime)
        where TState : IAsyncStateMachine;
}