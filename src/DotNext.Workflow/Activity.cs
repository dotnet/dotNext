namespace DotNext.Workflow;

public abstract class Activity
{
    protected abstract ActivityResult ExecuteAsync();
}