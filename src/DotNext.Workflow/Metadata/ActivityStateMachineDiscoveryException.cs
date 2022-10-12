using System.Runtime.CompilerServices;

namespace DotNext.Workflow.Metadata;

internal abstract class ActivityStateMachineDiscoveryException : Exception
{
    private protected ActivityStateMachineDiscoveryException()
    {
    }

    internal abstract ActivityMetaModel<TInput, TActivity> CreateMetaModel<TInput, TActivity>(Func<TActivity> factory, ActivityOptions options)
        where TInput : class
        where TActivity : Activity<TInput>;
}

internal sealed class ActivityStateMachineDiscoveryException<TStateMachine> : ActivityStateMachineDiscoveryException
    where TStateMachine : IAsyncStateMachine
{
    internal override ActivityMetaModel<TInput, TActivity, TStateMachine> CreateMetaModel<TInput, TActivity>(Func<TActivity> factory, ActivityOptions options)
        => new(factory, options);
}