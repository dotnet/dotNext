using System.Reflection;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Workflow.Metadata;

/// <summary>
/// Describes activity metadata model.
/// </summary>
internal abstract class ActivityMetaModel
{
    private protected const int InitialState = -1;

    internal readonly string Name;
    internal readonly ActivityOptions Options;

    private protected readonly FieldInfo stateMarkerField, builderField, contextField;
    private protected readonly FieldInfo? activityField;

    private protected ActivityMetaModel(Type activityType, Type stateMachineType, ActivityOptions options)
    {
        Debug.Assert(stateMachineType is not null);
        Debug.Assert(options is not null);

        Options = options;
        Name = GetActivityName(activityType);

        var fields = stateMachineType.GetFields(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public);
        foreach (var field in fields)
        {
            // C# compiler uses int field to store state marker
            if (field.FieldType == typeof(int))
                stateMarkerField = field;
            else if (field.FieldType == typeof(ActivityStateHandler))
                builderField = field;
            else if (field.FieldType.IsSubclassOf(typeof(ActivityContext)))
                contextField = field;
            else if (field.FieldType == activityType)
                activityField = field;
        }

        if (stateMarkerField is null)
            throw new ArgumentException();

        if (builderField is null)
            throw new ArgumentException();

        if (contextField is null)
            throw new ArgumentException();
    }

    /// <summary>
    /// Gets state machine type.
    /// </summary>
    internal Type StateMachineType => stateMarkerField.DeclaringType!;

    internal abstract bool Validate(IActivityStateValidator validator);

    internal abstract (IAsyncStateMachine, ActivityContext) PrepareForExecution(in ActivityInstance instance, IActivityStateProvider provider, WorkflowEngine engine, CancellationToken token);

    internal Activity? GetActivity<TState>(ref TState state)
        where TState : IAsyncStateMachine
        => activityField?.GetValueDirect(__makeref(state)) as Activity;

    internal ActivityContext? GetActivityContext<TState>(ref TState state)
        where TState : IAsyncStateMachine
        => contextField.GetValueDirect(__makeref(state)) as ActivityContext;

    internal void SetStateMarker<TState>(ref TState state, int value)
        where TState : IAsyncStateMachine
        => stateMarkerField.SetValueDirect(__makeref(state), value);

    internal void SetException<TState>(ref TState state, Exception e)
        where TState : IAsyncStateMachine
        => (builderField.GetValueDirect(__makeref(state)) as ActivityStateHandler)?.SetException(e);

    internal static string GetActivityName(Type activityType)
    {
        var result = activityType.Name;
        var index = result.LastIndexOf(nameof(Activity));
        return index > 0 ? result.Remove(index) : result;
    }
}

internal abstract class ActivityMetaModel<TInput> : ActivityMetaModel
    where TInput : class
{
    private protected ActivityMetaModel(Type activityType, Type stateMachineType, ActivityOptions options)
        : base(activityType, stateMachineType, options)
    {
    }

    internal sealed class InitialActivityStateProvider : Workflow.InitialActivityStateProvider
    {
        private readonly ActivityMetaModel model;
        private readonly object input;

        internal InitialActivityStateProvider(string instanceName, TInput input, ActivityMetaModel model)
        {
            Debug.Assert(input is not null);
            Debug.Assert(model is not null);
            Debug.Assert(instanceName is { Length: > 0 });

            this.model = model;
            this.input = input;
            InstanceName = instanceName;
        }

        public override (TExpectedInput, TExpectedState) GetRuntimeState<TExpectedInput, TExpectedState>()
        {
            var state = Activator.CreateInstance<TExpectedState>();
            model.SetStateMarker(ref state, InitialState);
            return ((TExpectedInput)input, state);
        }

        public override string InstanceName { get; }

        public override TimeSpan RemainingTime => model.Options.Timeout;

        internal override ValueTask InvokeCallback(IActivityStartedCallback callback, ActivityInstance instance)
            => callback.InvokeAsync(instance, input, model.Options);
    }
}

internal abstract class ActivityMetaModel<TInput, TActivity> : ActivityMetaModel<TInput>
    where TInput : class
    where TActivity : Activity<TInput>
{
    private protected readonly Func<TActivity> activityFactory;

    private protected ActivityMetaModel(Func<TActivity> factory, Type stateMachineType, ActivityOptions options)
        : base(typeof(TActivity), stateMachineType, options)
    {
        Debug.Assert(factory is not null);

        activityFactory = factory;
    }
}

internal sealed class ActivityMetaModel<TInput, TActivity, TState> : ActivityMetaModel<TInput, TActivity>
    where TInput : class
    where TActivity : Activity<TInput>
    where TState : IAsyncStateMachine
{
    internal ActivityMetaModel(Func<TActivity> factory, ActivityOptions options)
        : base(factory, typeof(TState), options)
    {
    }

    internal override bool Validate(IActivityStateValidator validator) => validator.Validate<TState>();

    internal override (IAsyncStateMachine, ActivityContext) PrepareForExecution(in ActivityInstance instance, IActivityStateProvider provider, WorkflowEngine engine, CancellationToken token)
    {
        var activity = activityFactory();
        (TInput input, IAsyncStateMachine activityState) = provider.GetRuntimeState<TInput, TState>();
        var builder = new ActivityStateHandler(this);
        var context = new ActivityContext<TInput>(input, activity, engine, in instance, provider.RemainingTime, token)
        {
            ActivityTask = builder.As<TaskCompletionSource>().Task
        };

        activityField?.SetValue(activityState, activity);
        contextField.SetValue(activityState, context);
        builderField.SetValue(activityState, builder);

        return (activityState, context);
    }
}