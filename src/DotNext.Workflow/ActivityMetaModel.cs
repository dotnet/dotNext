using System.Reflection;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Workflow;

internal sealed class ActivityMetaModel
{
    internal readonly Func<Activity> ActivityFactory;
    internal readonly string Name;
    internal readonly ActivityOptions Options;

    private readonly FieldInfo stateMarkerField, builderField, contextField;
    private readonly FieldInfo? tokenField, activityField;

    internal ActivityMetaModel(Func<Activity> factory, ActivityOptions options)
    {
        Debug.Assert(factory is not null);
        Debug.Assert(options is not null);

        Options = options;

        var activityType = factory.Method.ReturnType;
        ActivityFactory = factory;
        Name = Activity.GetName(activityType);

        // reflect state machine
        var stateMachineType = GetStateMachineType(activityType) ?? throw new ArgumentException();
        var fields = stateMachineType.GetFields(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var field in fields)
        {
            if (field.IsPublic)
            {
                // C# compiler uses int field to store state marker
                if (field.FieldType == typeof(int))
                    stateMarkerField = field;

                if (field.FieldType == typeof(ActivityBuilder))
                    builderField = field;

                if (field.FieldType.IsSubclassOf(typeof(ActivityContext)))
                    contextField = field;

                if (field.FieldType == activityType)
                    activityField = field;

                if (field.FieldType == typeof(CancellationToken))
                    tokenField = field;
            }
        }

        if (stateMarkerField is null)
            throw new ArgumentException();

        if (builderField is null)
            throw new ArgumentException();

        if (contextField is null)
            throw new ArgumentException();
    }

    private static Type? GetStateMachineType(Type activityType)
    {
        var rawActivity = RuntimeHelpers.GetUninitializedObject(activityType) as Activity;
        Debug.Assert(rawActivity is not null);

        return rawActivity.GetStateMachineType();
    }

    internal IAsyncStateMachine CreateStateMachine(int state, ActivityBuilder builder, ActivityContext context, Activity activity, CancellationToken token)
    {
        var stateMachine = Activator.CreateInstance(stateMarkerField.DeclaringType!) as IAsyncStateMachine;
        Debug.Assert(stateMachine is not null);

        stateMarkerField.SetValue(stateMachine, state);
        builderField.SetValue(stateMachine, builder);
        contextField.SetValue(stateMachine, context);
        activityField?.SetValue(stateMachine, activity);
        tokenField?.SetValue(stateMachine, token);

        return stateMachine;
    }

    internal ActivityContext? GetActivityContext<TState>(ref TState state)
        where TState : IAsyncStateMachine
        => contextField.GetValueDirect(__makeref(state)) as ActivityContext;
}