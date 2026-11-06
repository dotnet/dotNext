namespace DotNext.Threading;

internal interface IExecutionContextAction
{
    void Invoke();

    static virtual bool MayChangeSynchronizationContext => true;
}