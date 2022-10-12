namespace DotNext.Workflow;

public class InMemoryWorkflowEngine : WorkflowEngine
{
    internal protected override ValueTask ActivityCheckpointReachedAsync<TState>(ActivityInstance instance, TState executionState, TimeSpan remainingTime)
        => ValueTask.CompletedTask;

    protected override ValueTask ActivityCompleted(ActivityInstance instance, Exception? e)
        => ValueTask.CompletedTask;

    protected override ValueTask ActivityStarted<TInput>(ActivityInstance instance, TInput input, ActivityOptions options)
        => ValueTask.CompletedTask;
}