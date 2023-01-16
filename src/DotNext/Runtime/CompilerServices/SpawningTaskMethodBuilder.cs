using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

// TODO: Mark this type as file-local in C# 11
[StructLayout(LayoutKind.Auto)]
internal struct SpawningAsyncTaskMethodBuilderCore<TAsyncTaskBuilder>
    where TAsyncTaskBuilder : struct
{
    internal abstract class StateMachineContainer : IThreadPoolWorkItem
    {
        internal TAsyncTaskBuilder Builder;
        private ExecutionContext? context;
        private Action? moveNextAction;

        protected StateMachineContainer() => context = ExecutionContext.Capture();

        protected abstract void MoveNext();

        protected abstract bool IsCompleted { get; }

        private void MoveNextWithContext()
        {
            Debug.Assert(context is not null);

            ExecutionContext.Run(
                context,
                static stateMachine =>
                {
                    Debug.Assert(stateMachine is StateMachineContainer);

                    Unsafe.As<StateMachineContainer>(stateMachine).MoveNext();
                },
                this);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private Action CreateAction()
            => context is null ? MoveNext : MoveNextWithContext;

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
                MoveNext();
            else
                MoveNextWithContext();
        }
    }

    internal abstract class StateMachineContainer<TStateMachine> : StateMachineContainer
        where TStateMachine : notnull, IAsyncStateMachine
    {
        internal TStateMachine? StateMachine;

        protected sealed override void MoveNext()
        {
            Debug.Assert(IsCompleted is false);
            Debug.Assert(StateMachine is not null);

            StateMachine.MoveNext();

            if (IsCompleted)
                Cleanup();
        }

        private new void Cleanup()
        {
            StateMachine = default;
            base.Cleanup();
        }
    }

    private StateMachineContainer? container;

    internal readonly ref TAsyncTaskBuilder Builder
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            Debug.Assert(container is not null);

            return ref container.Builder;
        }
    }

    private TContainer GetContainer<TStateMachine, TContainer>(ref TStateMachine stateMachine)
        where TStateMachine : notnull, IAsyncStateMachine
        where TContainer : StateMachineContainer<TStateMachine>, new()
    {
        if (container is not TContainer result)
        {
            result = new();

            // modify builder first before storing state machine
            container = result;
            result.StateMachine = stateMachine;
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AwaitOnCompleted<TAwaiter, TStateMachine, TContainer>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : notnull, INotifyCompletion
        where TStateMachine : notnull, IAsyncStateMachine
        where TContainer : StateMachineContainer<TStateMachine>, new()
        => awaiter.OnCompleted(GetContainer<TStateMachine, TContainer>(ref stateMachine).MoveNextAction);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine, TContainer>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : notnull, ICriticalNotifyCompletion
        where TStateMachine : notnull, IAsyncStateMachine
        where TContainer : StateMachineContainer<TStateMachine>, new()
        => awaiter.UnsafeOnCompleted(GetContainer<TStateMachine, TContainer>(ref stateMachine).MoveNextAction);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Start<TStateMachine, TContainer>(ref TStateMachine stateMachine)
        where TStateMachine : notnull, IAsyncStateMachine
        where TContainer : StateMachineContainer<TStateMachine>, new()
    {
        IThreadPoolWorkItem workItem = GetContainer<TStateMachine, TContainer>(ref stateMachine);
        var scheduler = TaskScheduler.Current;

        if (ReferenceEquals(scheduler, TaskScheduler.Default))
        {
            ThreadPool.UnsafeQueueUserWorkItem(workItem, preferLocal: false);
        }
        else
        {
            Task.Factory.StartNew(
                static workItem =>
                {
                    Debug.Assert(workItem is IThreadPoolWorkItem);
                    Unsafe.As<IThreadPoolWorkItem>(workItem).Execute();
                },
                workItem,
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
public struct SpawningAsyncTaskMethodBuilder<TResult>
{
    private SpawningAsyncTaskMethodBuilderCore<AsyncTaskMethodBuilder<TResult>> core;

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
        => core.Start<TStateMachine, StateMachineContainer<TStateMachine>>(ref stateMachine);

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
        => core.Start<TStateMachine, StateMachineContainer<TStateMachine>>(ref stateMachine);

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
        => core.Start<TStateMachine, StateMachineContainer<TStateMachine>>(ref stateMachine);

    /// <summary>
    /// Associates the builder with the state machine it represents.
    /// </summary>
    /// <param name="stateMachine">The heap-allocated state machine object.</param>
    public readonly void SetStateMachine(IAsyncStateMachine stateMachine)
    {
        // This method is not used by C# compiler
    }

    /// <summary>
    /// Completes asynchronous operation successfully.
    /// </summary>
    /// <param name="result">The result to be returned by the asynchronous method associated with this builder.</param>
    public readonly void SetResult(TResult result)
        => core.Builder.SetResult(result);

    /// <summary>
    /// Completes asynchronous operation unsuccessfully.
    /// </summary>
    /// <param name="e">The exception to be thrown by the asynchronous method associated with this builder.</param>
    public readonly void SetException(Exception e)
        => core.Builder.SetException(e);

    /// <summary>
    /// Gets the task representing the builder's asynchronous operation.
    /// </summary>
    public readonly Task<TResult> Task => core.Builder.Task;

    private sealed class StateMachineContainer<TStateMachine> : SpawningAsyncTaskMethodBuilderCore<AsyncTaskMethodBuilder<TResult>>.StateMachineContainer<TStateMachine>
        where TStateMachine : notnull, IAsyncStateMachine
    {
        public StateMachineContainer()
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
public struct SpawningAsyncTaskMethodBuilder
{
    private SpawningAsyncTaskMethodBuilderCore<AsyncTaskMethodBuilder> core;

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
        => core.Start<TStateMachine, StateMachineContainer<TStateMachine>>(ref stateMachine);

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
        => core.AwaitOnCompleted<TAwaiter, TStateMachine, StateMachineContainer<TStateMachine>>(ref awaiter, ref stateMachine);

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
        => core.AwaitUnsafeOnCompleted<TAwaiter, TStateMachine, StateMachineContainer<TStateMachine>>(ref awaiter, ref stateMachine);

    /// <summary>
    /// Associates the builder with the state machine it represents.
    /// </summary>
    /// <param name="stateMachine">The heap-allocated state machine object.</param>
    public readonly void SetStateMachine(IAsyncStateMachine stateMachine)
    {
        // This method is not used by C# compiler
    }

    /// <summary>
    /// Completes asynchronous operation successfully.
    /// </summary>
    public readonly void SetResult()
        => core.Builder.SetResult();

    /// <summary>
    /// Completes asynchronous operation unsuccessfully.
    /// </summary>
    /// <param name="e">The exception to be thrown by the asynchronous method associated with this builder.</param>
    public readonly void SetException(Exception e)
        => core.Builder.SetException(e);

    /// <summary>
    /// Gets the task representing the builder's asynchronous operation.
    /// </summary>
    public readonly Task Task => core.Builder.Task;

    private sealed class StateMachineContainer<TStateMachine> : SpawningAsyncTaskMethodBuilderCore<AsyncTaskMethodBuilder>.StateMachineContainer<TStateMachine>
        where TStateMachine : notnull, IAsyncStateMachine
    {
        public StateMachineContainer()
        {
            // ensure that internal builder has initialized task to avoid race condition
            // between Task returned immediately after Start and SetResult in a different thread
            GC.KeepAlive(Builder.Task);
        }

        protected override bool IsCompleted => Builder.Task.IsCompleted;
    }
}