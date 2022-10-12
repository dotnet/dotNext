using IAsyncStateMachine = System.Runtime.CompilerServices.IAsyncStateMachine;

namespace DotNext.Workflow;

internal interface IActivityStateValidator
{
    bool Validate<TState>()
        where TState : IAsyncStateMachine;
}