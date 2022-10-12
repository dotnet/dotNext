using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Workflow;

using Metadata;

/// <summary>
/// Represents activity runtime state builder.
/// </summary>
/// <remarks>
/// This class is for internal purposes only.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ActivityStateHandler : TaskCompletionSource
{
    private readonly ActivityMetaModel metaModel;
    private IAsyncStateMachine? box;
    private ExecutionContext? context;
    private Action? moveNextAction;

    internal ActivityStateHandler(ActivityMetaModel metaModel)
        : base(TaskCreationOptions.RunContinuationsAsynchronously)
    {
        Debug.Assert(metaModel is not null);

        this.metaModel = metaModel;
    }

    /// <summary>
    /// Always throws <see cref="NotImplementedException"/>.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    [Obsolete("This method is not allowed to be called directly", error: true)]
    public static ActivityStateHandler? Create() => RuntimeHelpers.GetUninitializedObject(typeof(ActivityStateHandler)) as ActivityStateHandler;

    public new ActivityResult Task => new(base.Task);

    private void Cleanup()
    {
        box = null;
        context = null;
        moveNextAction = null;
    }

    /// <summary>
    /// Informs that the activity has been completed unsuccessfully.
    /// </summary>
    /// <param name="e">The exception to be associated with the activity.</param>
    public new void SetException(Exception e)
    {
        Cleanup();

        if (e is OperationCanceledException canceledEx)
            base.SetCanceled(canceledEx.CancellationToken);
        else
            base.SetException(e);
    }

    /// <summary>
    /// Informs that the activity has been completed successfully.
    /// </summary>
    public new void SetResult()
    {
        Cleanup();
        base.SetResult();
    }

    private static async Task CheckpointReachedAsync<TState>(ActivityContext context, TState state)
        where TState : IAsyncStateMachine
    {
        Debug.Assert(context is not null);

        if (context.RemainingTime.RemainingTime.TryGetValue(out var remainingTime))
        {
            await context.Engine.ActivityCheckpointReachedAsync(context.Instance, state, remainingTime).ConfigureAwait(false);
            await context.OnCheckpoint().ConfigureAwait(false);
        }
        else
        {
            throw new TimeoutException();
        }
    }

    private void CheckpointReachedAsync<TState>(ref CheckpointResult.Awaiter awaiter, ref TState state, bool unsafeCompletion)
        where TState : IAsyncStateMachine
    {
        var context = metaModel.GetActivityContext(ref state);
        Debug.Assert(context is not null);

        var persistenceTask = CheckpointReachedAsync(context, state);
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
        {
            CheckpointReachedAsync(ref Unsafe.As<TAwaiter, CheckpointResult.Awaiter>(ref awaiter), ref stateMachine, unsafeCompletion: false);
        }
        else
        {
            SetStateMachineBox(ref stateMachine);
            awaiter.OnCompleted(moveNextAction);
        }
    }

    public void AwaitUnsafeOnCompleted<TAwaiter, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine
    {
        if (typeof(TAwaiter) == typeof(CheckpointResult.Awaiter))
        {
            CheckpointReachedAsync(ref Unsafe.As<TAwaiter, CheckpointResult.Awaiter>(ref awaiter), ref stateMachine, unsafeCompletion: true);
        }
        else
        {
            SetStateMachineBox(ref stateMachine);
            awaiter.OnCompleted(moveNextAction);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [Obsolete("This method is not allowed to be called directly", error: true)]
    public void Start<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : IAsyncStateMachine
        => throw new ActivityStateMachineDiscoveryException<TStateMachine>(); // capture actual generic type

    /// <summary>
    /// Associates activity state machine with this builder.
    /// </summary>
    /// <param name="value">The state machine.</param>
    public void SetStateMachine(IAsyncStateMachine value)
        => SetStateMachineBox(ref value);
}