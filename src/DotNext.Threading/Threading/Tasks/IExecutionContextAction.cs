namespace DotNext.Threading.Tasks;

internal interface IExecutionContextAction
{
    void Invoke();

    static virtual bool MayChangeSynchronizationContext => true;
}