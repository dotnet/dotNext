using System.Runtime.CompilerServices;

namespace DotNext.Workflow;

public struct ActivityBuilder
{
    public static ActivityBuilder Create() => new();

    public ActivityResult Task => default;

    public void SetException(Exception e)
    {

    }

    public void SetResult()
    {

    }

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine
    {

    }

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine
    {        
    }

    public void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : IAsyncStateMachine
    {
    }

    public void SetStateMachine(IAsyncStateMachine stateMachine)
    {
    }
}