using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Threading.Tasks;

/// <summary>
/// Represents timer-based scheduler.
/// </summary>
public static partial class TaskSchedulerExtensions
{
    /// <summary>
    /// Extends <see cref="System.Threading.Tasks.TaskScheduler"/> type.
    /// </summary>
    extension(TaskScheduler)
    {
        /// <summary>
        /// Schedules the specific action to be executed once after the specified delay.
        /// </summary>
        /// <typeparam name="TArgs">The type of arguments to be passed to the callback.</typeparam>
        /// <param name="callback">The callback to be executed after the specified delay.</param>
        /// <param name="args">The arguments to be passed to the callback.</param>
        /// <param name="delay">The amount of time used to delay callback execution.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing delayed execution.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="delay"/> is less than <see cref="TimeSpan.Zero"/> and not equal to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>.</exception>
        public static DelayedTask ScheduleAsync<TArgs>(Func<TArgs, CancellationToken, ValueTask> callback, TArgs args, TimeSpan delay,
            CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(callback);
            Timeout.Validate(delay);

            return delay.Ticks is 0L
                ? new ImmediateTask<TArgs>(callback, args, token)
                : DelayedTaskStateMachine<TArgs>.Start(callback, args, delay, token);
        }
    }
}

file sealed class ImmediateTask<TArgs> : DelayedTask
{
    internal ImmediateTask(Func<TArgs, CancellationToken, ValueTask> callback, TArgs args, CancellationToken token)
        : base(token)
        => Task = callback(args, this.token).AsTask();

    public override Task Task { get; }

    private protected override void SetException(Exception e) => Debug.Fail("Should not be called");

    private protected override void AdvanceStateMachine() => Debug.Fail("Should not be called");
}

file sealed class DelayedTaskStateMachine<TArgs> : DelayedTask
{
    private readonly Func<TArgs, CancellationToken, ValueTask> callback;
    private readonly TArgs args;
    private readonly TimeSpan delay;
    private AsyncTaskMethodBuilder builder;
    private ConfiguredTaskAwaitable.ConfiguredTaskAwaiter delayAwaiter;
    private ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter callbackAwaiter;

    private DelayedTaskStateMachine(Func<TArgs, CancellationToken, ValueTask> callback, TArgs args, TimeSpan delay, CancellationToken token)
        : base(token)
    {
        Debug.Assert(callback is not null);

        this.callback = callback;
        this.delay = delay;
        this.args = args;
        builder = AsyncTaskMethodBuilder.Create();
        GC.KeepAlive(builder.Task); // initialize promise task immediately
    }

    internal static DelayedTask Start(Func<TArgs, CancellationToken, ValueTask> callback, TArgs args, TimeSpan delay, CancellationToken token)
    {
        var machine = new DelayedTaskStateMachine<TArgs>(callback, args, delay, token);
        Start(machine);
        return machine;
    }

    public override Task Task => builder.Task;

    private protected override void AdvanceStateMachine()
    {
        switch (state)
        {
            case InitialState:
                delayAwaiter = Task.Delay(delay, token).ConfigureAwait(false).GetAwaiter();
                state = DelayState;
                if (delayAwaiter.IsCompleted)
                    goto case DelayState;
                Await(ref delayAwaiter);
                break;
            case DelayState:
                GetResultAndClear(ref delayAwaiter);
                callbackAwaiter = callback.Invoke(args, token).ConfigureAwait(false).GetAwaiter();
                if (callbackAwaiter.IsCompleted)
                    goto default;
                state = DelayState + 1U;
                Await(ref callbackAwaiter);
                break;
            default:
                GetResultAndClear(ref callbackAwaiter);
                delayAwaiter = default;
                Cleanup();
                builder.SetResult();
                break;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GetResultAndClear(ref ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter awaiter)
    {
        var awaiterCopy = awaiter;
        awaiter = default;
        awaiterCopy.GetResult();
    }

    private protected override void SetException(Exception e) => builder.SetException(e);
}