using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

internal interface ISpawningAsyncTaskMethodBuilder<TAsyncTaskBuilder>
    where TAsyncTaskBuilder : struct
{
    protected abstract class StateMachineContainer : IThreadPoolWorkItem
    {
        internal TAsyncTaskBuilder Builder;
        private ExecutionContext? context;
        private Action? moveNextAction;

        protected StateMachineContainer() => context = ExecutionContext.Capture();

        protected abstract void MoveNextWithoutContext();

        protected abstract bool IsCompleted { get; }

        private void MoveNextWithContext()
        {
            Debug.Assert(context is not null);

            ExecutionContext.Run(
                context,
                static stateMachine =>
                {
                    Debug.Assert(stateMachine is StateMachineContainer);

                    Unsafe.As<StateMachineContainer>(stateMachine).MoveNextWithoutContext();
                },
                this);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private Action CreateAction()
            => context is null ? MoveNextWithoutContext : MoveNextWithContext;

        internal Action MoveNextAction => moveNextAction ??= CreateAction();

        protected void Cleanup()
        {
            Debug.Assert(IsCompleted);

            context = null;
            moveNextAction = null;
        }

        void IThreadPoolWorkItem.Execute()
        {
            if (context is null)
                MoveNextWithoutContext();
            else
                MoveNextWithContext();
        }
    }

    protected abstract class StateMachineContainer<TStateMachine> : StateMachineContainer
        where TStateMachine : notnull, IAsyncStateMachine
    {
        private TStateMachine stateMachine;

        protected StateMachineContainer(ref TStateMachine stateMachine)
            => this.stateMachine = stateMachine;

        protected sealed override void MoveNextWithoutContext()
        {
            Debug.Assert(IsCompleted is false);

            stateMachine.MoveNext();

            if (IsCompleted)
                Cleanup();
        }

        private new void Cleanup()
        {
            stateMachine = default!;
            base.Cleanup();
        }
    }

    protected StateMachineContainer<TStateMachine> GetContainer<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : notnull, IAsyncStateMachine;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void AwaitOnCompleted<TBuilder, TAwaiter, TStateMachine>(ref TBuilder builder, ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TBuilder : struct, ISpawningAsyncTaskMethodBuilder<TAsyncTaskBuilder>
        where TAwaiter : notnull, INotifyCompletion
        where TStateMachine : notnull, IAsyncStateMachine
        => awaiter.OnCompleted(builder.GetContainer(ref stateMachine).MoveNextAction);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void AwaitUnsafeOnCompleted<TBuilder, TAwaiter, TStateMachine>(ref TBuilder builder, ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TBuilder : struct, ISpawningAsyncTaskMethodBuilder<TAsyncTaskBuilder>
        where TAwaiter : notnull, ICriticalNotifyCompletion
        where TStateMachine : notnull, IAsyncStateMachine
        => awaiter.UnsafeOnCompleted(builder.GetContainer(ref stateMachine).MoveNextAction);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void Start<TBuilder, TStateMachine>(ref TBuilder builder, ref TStateMachine stateMachine)
        where TBuilder : struct, ISpawningAsyncTaskMethodBuilder<TAsyncTaskBuilder>
        where TStateMachine : notnull, IAsyncStateMachine
    {
        var container = builder.GetContainer(ref stateMachine);
        var scheduler = TaskScheduler.Current;

        if (ReferenceEquals(scheduler, TaskScheduler.Default))
        {
            ThreadPool.UnsafeQueueUserWorkItem(container, preferLocal: false);
        }
        else
        {
            Task.Factory.StartNew(
                static workItem =>
                {
                    Debug.Assert(workItem is IThreadPoolWorkItem);
                    Unsafe.As<IThreadPoolWorkItem>(workItem).Execute();
                },
                container,
                CancellationToken.None,
                TaskCreationOptions.PreferFairness,
                scheduler);
        }
    }
}

/// <summary>
/// When applied to async method using <see cref="AsyncMethodBuilderAttribute"/> attribute,
/// spawns method execution as a new work item in the thread pool, i.e. in parallel.
/// </summary>
/// <remarks>
/// This builder has the same effect as <see cref="Task.Run{TResult}(Func{Task{TResult}?})"/> but consumes
/// less memory.
/// </remarks>
/// <typeparam name="TResult">The type of the value to be returned by asynchronous method.</typeparam>
[StructLayout(LayoutKind.Auto)]
public struct SpawningAsyncTaskMethodBuilder<TResult> : ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder<TResult>>
{
    private ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder<TResult>>.StateMachineContainer? container;

    /// <summary>
    /// Initializes a new builder.
    /// </summary>
    /// <returns>A new builder.</returns>
    public static SpawningAsyncTaskMethodBuilder<TResult> Create() => default;

    /// <summary>
    /// Initiates the builder's execution with the associated state machine.
    /// </summary>
    /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
    /// <param name="stateMachine">The state machine instance, passed by reference.</param>
    public void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : notnull, IAsyncStateMachine
        => ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder<TResult>>.Start(ref this, ref stateMachine);

    /// <summary>
    /// Schedules the specified state machine to be pushed forward when the specified awaiter completes.
    /// </summary>
    /// <typeparam name="TAwaiter">Specifies the type of the awaiter.</typeparam>
    /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
    /// <param name="awaiter">The awaiter.</param>
    /// <param name="stateMachine">The state machine.</param>
    public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : notnull, INotifyCompletion
        where TStateMachine : notnull, IAsyncStateMachine
        => ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder<TResult>>.AwaitOnCompleted(ref this, ref awaiter, ref stateMachine);

    /// <summary>
    /// Schedules the specified state machine to be pushed forward when the specified awaiter completes;
    /// without capturing execution context.
    /// </summary>
    /// <typeparam name="TAwaiter">Specifies the type of the awaiter.</typeparam>
    /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
    /// <param name="awaiter">The awaiter.</param>
    /// <param name="stateMachine">The state machine.</param>
    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : notnull, ICriticalNotifyCompletion
        where TStateMachine : notnull, IAsyncStateMachine
        => ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder<TResult>>.AwaitUnsafeOnCompleted(ref this, ref awaiter, ref stateMachine);

    /// <summary>
    /// Associates the builder with the state machine it represents.
    /// </summary>
    /// <param name="stateMachine">The heap-allocated state machine object.</param>
    public readonly void SetStateMachine(IAsyncStateMachine stateMachine)
    {
        // This method is not used by C# compiler
    }

    /// <inheritdoc />
    ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder<TResult>>.StateMachineContainer<TStateMachine> ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder<TResult>>.GetContainer<TStateMachine>(ref TStateMachine stateMachine)
    {
        if (container is not StateMachineContainer<TStateMachine> result)
        {
            result = new(ref stateMachine);
            container = result;
        }

        return result;
    }

    /// <summary>
    /// Completes asynchronous operation successfully.
    /// </summary>
    /// <param name="result">The result to be returned by the asynchronous method associated with this builder.</param>
    public void SetResult(TResult result)
        => container?.Builder.SetResult(result);

    /// <summary>
    /// Completes asynchronous operation unsuccessfully.
    /// </summary>
    /// <param name="e">The exception to be thrown by the asynchronous method associated with this builder.</param>
    public void SetException(Exception e)
        => container?.Builder.SetException(e);

    /// <summary>
    /// Gets the task representing the builder's asynchronous operation.
    /// </summary>
    public Task<TResult> Task
    {
        get
        {
            Debug.Assert(container is not null);

            return container.Builder.Task;
        }
    }

    private sealed class StateMachineContainer<TStateMachine> : ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder<TResult>>.StateMachineContainer<TStateMachine>
        where TStateMachine : notnull, IAsyncStateMachine
    {
        internal StateMachineContainer(ref TStateMachine stateMachine)
            : base(ref stateMachine)
        {
            // ensure that internal builder has initialized task to avoid race condition
            // between Task returned immediately after Start and SetResult in a different thread
            GC.KeepAlive(Builder.Task);
        }

        protected override bool IsCompleted => Builder.Task.IsCompleted;
    }
}

/// <summary>
/// When applied to async method using <see cref="AsyncMethodBuilderAttribute"/> attribute,
/// spawns method execution as a new work item in the thread pool, i.e. in parallel.
/// </summary>
/// <remarks>
/// This builder has the same effect as <see cref="Task.Run(Func{Task?})"/> but consumes
/// less memory.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public struct SpawningAsyncTaskMethodBuilder : ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder>
{
    private ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder>.StateMachineContainer? container;

    /// <summary>
    /// Initializes a new builder.
    /// </summary>
    /// <returns>A new builder.</returns>
    public static SpawningAsyncTaskMethodBuilder Create() => default;

    /// <summary>
    /// Initiates the builder's execution with the associated state machine.
    /// </summary>
    /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
    /// <param name="stateMachine">The state machine instance, passed by reference.</param>
    public void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : notnull, IAsyncStateMachine
        => ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder>.Start(ref this, ref stateMachine);

    /// <summary>
    /// Schedules the specified state machine to be pushed forward when the specified awaiter completes.
    /// </summary>
    /// <typeparam name="TAwaiter">Specifies the type of the awaiter.</typeparam>
    /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
    /// <param name="awaiter">The awaiter.</param>
    /// <param name="stateMachine">The state machine.</param>
    public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : notnull, INotifyCompletion
        where TStateMachine : notnull, IAsyncStateMachine
        => ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder>.AwaitOnCompleted(ref this, ref awaiter, ref stateMachine);

    /// <summary>
    /// Schedules the specified state machine to be pushed forward when the specified awaiter completes;
    /// without capturing execution context.
    /// </summary>
    /// <typeparam name="TAwaiter">Specifies the type of the awaiter.</typeparam>
    /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
    /// <param name="awaiter">The awaiter.</param>
    /// <param name="stateMachine">The state machine.</param>
    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : notnull, ICriticalNotifyCompletion
        where TStateMachine : notnull, IAsyncStateMachine
        => ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder>.AwaitUnsafeOnCompleted(ref this, ref awaiter, ref stateMachine);

    /// <summary>
    /// Associates the builder with the state machine it represents.
    /// </summary>
    /// <param name="stateMachine">The heap-allocated state machine object.</param>
    public readonly void SetStateMachine(IAsyncStateMachine stateMachine)
    {
        // This method is not used by C# compiler
    }

    /// <inheritdoc />
    ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder>.StateMachineContainer<TStateMachine> ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder>.GetContainer<TStateMachine>(ref TStateMachine stateMachine)
    {
        if (container is not StateMachineContainer<TStateMachine> result)
        {
            result = new(ref stateMachine);
            container = result;
        }

        return result;
    }

    /// <summary>
    /// Completes asynchronous operation successfully.
    /// </summary>
    public void SetResult()
        => container?.Builder.SetResult();

    /// <summary>
    /// Completes asynchronous operation unsuccessfully.
    /// </summary>
    /// <param name="e">The exception to be thrown by the asynchronous method associated with this builder.</param>
    public void SetException(Exception e)
        => container?.Builder.SetException(e);

    /// <summary>
    /// Gets the task representing the builder's asynchronous operation.
    /// </summary>
    public Task Task
    {
        get
        {
            Debug.Assert(container is not null);

            return container.Builder.Task;
        }
    }

    private sealed class StateMachineContainer<TStateMachine> : ISpawningAsyncTaskMethodBuilder<AsyncTaskMethodBuilder>.StateMachineContainer<TStateMachine>
        where TStateMachine : notnull, IAsyncStateMachine
    {
        internal StateMachineContainer(ref TStateMachine stateMachine)
            : base(ref stateMachine)
        {
            // ensure that internal builder has initialized task to avoid race condition
            // between Task returned immediately after Start and SetResult in a different thread
            GC.KeepAlive(Builder.Task);
        }

        protected override bool IsCompleted => Builder.Task.IsCompleted;
    }
}