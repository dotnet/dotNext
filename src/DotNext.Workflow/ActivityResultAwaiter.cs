using System.Runtime.CompilerServices;

namespace DotNext.Workflow;

public struct ActivityResultAwaiter : INotifyCompletion
{
    public bool IsCompleted => false;

    public void GetResult() => throw new NotImplementedException();

    void INotifyCompletion.OnCompleted(Action continuation) => throw new NotImplementedException();
}