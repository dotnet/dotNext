using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Workflow;

/// <summary>
/// Represents activity builder.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ActivityBuilder : TaskCompletionSource
{
    internal const int InitialState = -1;
    internal const int FinalState = -2;
    private readonly ICheckpointCallback checkpointCallback;
    private readonly ActivityMetaModel metaModel;
    private IAsyncStateMachine? box;
    private ExecutionContext? context;
    private Action? moveNextAction;

    internal ActivityBuilder(ICheckpointCallback checkpointCallback, ActivityMetaModel metaModel)
        : base(TaskCreationOptions.RunContinuationsAsynchronously)
    {
        Debug.Assert(checkpointCallback is not null);
        Debug.Assert(metaModel is not null);

        this.checkpointCallback = checkpointCallback;
        this.metaModel = metaModel;
    }

    internal string ActivityName => metaModel.Name;

    internal ActivityOptions Options => metaModel.Options;

    /// <summary>
    /// Always throws <see cref="NotImplementedException"/>.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ActivityBuilder Create() => throw new NotImplementedException();

    public new ActivityResult Task => new(base.Task);

    private void Cleanup()
    {
        box = null;
        context = null;
        moveNextAction = null;
    }

    public new void SetException(Exception e)
    {
        Cleanup();

        if (e is OperationCanceledException canceledEx)
            base.SetCanceled(canceledEx.CancellationToken);
        else
            base.SetException(e);
    }

    public new void SetResult()
    {
        Cleanup();
        base.SetResult();
    }

    private async Task PersistStateAsync<TState>(TState state)
        where TState : IAsyncStateMachine
    {
        var context = metaModel.GetActivityContext(ref state);
        Debug.Assert(context is not null);

        if (context.RemainingTime.RemainingTime.TryGetValue(out var remainingTime))
        {
            await checkpointCallback.CheckpointReachedAsync(new(metaModel.Name, context.InstanceName), state, remainingTime).ConfigureAwait(false);
            await context.OnCheckpoint().ConfigureAwait(false);
        }
        else
        {
            throw new TimeoutException();
        }
    }

    private void PersistState<TState>(ref CheckpointResult.Awaiter awaiter, ref TState state, bool unsafeCompletion)
        where TState : IAsyncStateMachine
    {
        var persistenceTask = PersistStateAsync(state);

        if (awaiter.SetResult(persistenceTask))
        {
            // completed synchronously, advances state machine
            state.MoveNext();
        }
        else
        {
            // completed asynchronously, wait for completion
            SetStateMachineBox(ref state);
            var persistenceTaskAwaiter = persistenceTask.ConfigureAwait(false).GetAwaiter();

            if (unsafeCompletion)
                persistenceTaskAwaiter.UnsafeOnCompleted(moveNextAction);
            else
                persistenceTaskAwaiter.OnCompleted(moveNextAction);
        }
    }

    private void MoveNext()
    {
        Debug.Assert(box is not null);

        if (context is null)
        {
            box.MoveNext();
        }
        else
        {
            ExecutionContext.Run(context, static box => Unsafe.As<IAsyncStateMachine>(box!).MoveNext(), box);
        }
    }

    [MemberNotNull(nameof(box))]
    [MemberNotNull(nameof(moveNextAction))]
    private void SetStateMachineBox<TState>(ref TState state)
        where TState : IAsyncStateMachine
    {
        box ??= state;
        moveNextAction ??= MoveNext;

        var context = ExecutionContext.Capture();
        if (!ReferenceEquals(this.context, context))
            this.context = context;
    }

    public void AwaitOnCompleted<TAwaiter, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine
    {
        if (typeof(TAwaiter) == typeof(CheckpointResult.Awaiter))
            PersistState(ref Unsafe.As<TAwaiter, CheckpointResult.Awaiter>(ref awaiter), ref stateMachine, unsafeCompletion: false);

        SetStateMachineBox(ref stateMachine);
        awaiter.OnCompleted(moveNextAction);
    }

    public void AwaitUnsafeOnCompleted<TAwaiter, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine
    {
        if (typeof(TAwaiter) == typeof(CheckpointResult.Awaiter))
            PersistState(ref Unsafe.As<TAwaiter, CheckpointResult.Awaiter>(ref awaiter), ref stateMachine, unsafeCompletion: true);

        SetStateMachineBox(ref stateMachine);
        awaiter.UnsafeOnCompleted(moveNextAction);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Start<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : IAsyncStateMachine
        => throw new NotImplementedException();

    public void SetStateMachine(IAsyncStateMachine stateMachine)
        => box = stateMachine;
}