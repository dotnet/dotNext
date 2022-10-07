namespace DotNext.Workflow;

public sealed class DuplicateWorkflowInstanceException : WorkflowExecutionException
{
    internal DuplicateWorkflowInstanceException(in ActivityInstance instance)
        => Instance = instance;

    public ActivityInstance Instance { get; }
}