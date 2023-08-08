using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

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
    private AsyncTaskMethodBuilder<TResult> builder;

    /// <summary>
    /// Initializes a new builder.
    /// </summary>
    public SpawningAsyncTaskMethodBuilder() => builder = AsyncTaskMethodBuilder<TResult>.Create();

    /// <summary>
    /// Initializes a new builder.
    /// </summary>
    /// <returns>A new builder.</returns>
    public static SpawningAsyncTaskMethodBuilder<TResult> Create() => new();

    /// <summary>
    /// Initiates the builder's execution with the associated state machine.
    /// </summary>
    /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
    /// <param name="stateMachine">The state machine instance, passed by reference.</param>
    public void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : notnull, IAsyncStateMachine
    {
        // force builder to initialize state machine box
        var workItem = new ContinuationWorkItem();
        builder.AwaitOnCompleted(ref workItem, ref stateMachine);
        ThreadPool.UnsafeQueueUserWorkItem(workItem, preferLocal: true);
    }

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
        => builder.AwaitOnCompleted(ref awaiter, ref stateMachine);

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
        => builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);

    /// <summary>
    /// Associates the builder with the state machine it represents.
    /// </summary>
    /// <param name="stateMachine">The heap-allocated state machine object.</param>
    public void SetStateMachine(IAsyncStateMachine stateMachine)
        => builder.SetStateMachine(stateMachine);

    /// <summary>
    /// Completes asynchronous operation successfully.
    /// </summary>
    /// <param name="result">The result to be returned by the asynchronous method associated with this builder.</param>
    public readonly void SetResult(TResult result)
        => builder.SetResult(result);

    /// <summary>
    /// Completes asynchronous operation unsuccessfully.
    /// </summary>
    /// <param name="e">The exception to be thrown by the asynchronous method associated with this builder.</param>
    public void SetException(Exception e)
        => builder.SetException(e);

    /// <summary>
    /// Gets the task representing the builder's asynchronous operation.
    /// </summary>
    public Task<TResult> Task => builder.Task;
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
    private AsyncTaskMethodBuilder builder;

    /// <summary>
    /// Initializes a new builder.
    /// </summary>
    public SpawningAsyncTaskMethodBuilder() => builder = AsyncTaskMethodBuilder.Create();

    /// <summary>
    /// Initializes a new builder.
    /// </summary>
    /// <returns>A new builder.</returns>
    public static SpawningAsyncTaskMethodBuilder Create() => new();

    /// <summary>
    /// Initiates the builder's execution with the associated state machine.
    /// </summary>
    /// <typeparam name="TStateMachine">Specifies the type of the state machine.</typeparam>
    /// <param name="stateMachine">The state machine instance, passed by reference.</param>
    public void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : notnull, IAsyncStateMachine
    {
        // force builder to initialize state machine box
        var workItem = new ContinuationWorkItem();
        builder.AwaitOnCompleted(ref workItem, ref stateMachine);
        ThreadPool.UnsafeQueueUserWorkItem(workItem, preferLocal: true);
    }

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
        => builder.AwaitOnCompleted(ref awaiter, ref stateMachine);

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
        => builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);

    /// <summary>
    /// Associates the builder with the state machine it represents.
    /// </summary>
    /// <param name="stateMachine">The heap-allocated state machine object.</param>
    public void SetStateMachine(IAsyncStateMachine stateMachine)
        => builder.SetStateMachine(stateMachine);

    /// <summary>
    /// Completes asynchronous operation successfully.
    /// </summary>
    public void SetResult()
        => builder.SetResult();

    /// <summary>
    /// Completes asynchronous operation unsuccessfully.
    /// </summary>
    /// <param name="e">The exception to be thrown by the asynchronous method associated with this builder.</param>
    public void SetException(Exception e)
        => builder.SetException(e);

    /// <summary>
    /// Gets the task representing the builder's asynchronous operation.
    /// </summary>
    public Task Task => builder.Task;
}

// TODO: Convert to file-local type
[StructLayout(LayoutKind.Auto)]
internal struct ContinuationWorkItem : INotifyCompletion, IThreadPoolWorkItem
{
    private Action? moveNext;

    void INotifyCompletion.OnCompleted(Action continuation)
        => moveNext = continuation;

    readonly void IThreadPoolWorkItem.Execute() => moveNext?.Invoke();
}