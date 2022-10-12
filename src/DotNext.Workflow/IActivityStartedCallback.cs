namespace DotNext.Workflow;

internal interface IActivityStartedCallback
{
    ValueTask InvokeAsync<TInput>(ActivityInstance instance, TInput input, ActivityOptions options)
        where TInput : class;
}