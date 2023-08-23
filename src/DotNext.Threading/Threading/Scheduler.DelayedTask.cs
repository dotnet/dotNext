using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading;

/// <summary>
/// Represents timer-based scheduler.
/// </summary>
public static partial class Scheduler
{
    /// <summary>
    /// Represents a task with delayed completion.
    /// </summary>
    public abstract class DelayedTask
    {
        private protected const uint InitialState = 0U;
        private protected const uint DelayState = 1U;

        private protected readonly CancellationToken token; // cached token to avoid ObjectDisposedException
        private volatile CancellationTokenSource? tokenSource;
        private protected uint state;
        private Action? continuation;

        private protected DelayedTask(CancellationToken token)
            => this.token = (tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token)).Token;

        /// <summary>
        /// Gets delayed task.
        /// </summary>
        /// <seealso cref="DelayedTaskCanceledException"/>
        public abstract Task Task { get; }

        /// <summary>
        /// Cancels scheduled task.
        /// </summary>
        public void Cancel()
        {
            if (Interlocked.Exchange(ref tokenSource, null) is { } cts)
            {
                try
                {
                    cts.Cancel();
                }
                finally
                {
                    cts.Dispose();
                }
            }
        }

        private protected virtual void Cleanup() => Interlocked.Exchange(ref tokenSource, null)?.Dispose();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected static void GetResultAndClear(ref ConfiguredTaskAwaitable.ConfiguredTaskAwaiter awaiter)
        {
            awaiter.GetResult();
            awaiter = default;
        }

        /// <summary>
        /// Gets an awaiter used to await this task.
        /// </summary>
        /// <returns>An awaiter instance.</returns>
        public TaskAwaiter GetAwaiter() => Task.GetAwaiter();

        /// <summary>
        /// Configures an awaiter used to await this task.
        /// </summary>
        /// <param name="continueOnCapturedContext">
        /// <see langword="true"/> to attempt to marshal the continuation back to the original context captured;
        /// otherwise, <see langword="false"/>.
        /// </param>
        /// <returns>An awaiter instance.</returns>
        public ConfiguredTaskAwaitable ConfigureAwait(bool continueOnCapturedContext)
            => Task.ConfigureAwait(continueOnCapturedContext);

        private protected abstract void SetException(Exception e);

        private protected abstract void AdvanceStateMachine();

        private protected void Await<TAwaiter>(ref TAwaiter awaiter)
            where TAwaiter : struct, INotifyCompletion
            => awaiter.OnCompleted(continuation ??= MoveNext);

        private void MoveNext()
        {
            try
            {
                AdvanceStateMachine();
            }
            catch (Exception e)
            {
                Cleanup();

                if (state is DelayState && e is OperationCanceledException canceledEx)
                {
                    e = new DelayedTaskCanceledException(canceledEx);

                    if (canceledEx.StackTrace is { Length: > 0 } stackTrace)
                        ExceptionDispatchInfo.SetRemoteStackTrace(e, stackTrace);
                    else
                        ExceptionDispatchInfo.SetCurrentStackTrace(e);
                }

                SetException(e);
            }
        }

        private protected static void Start(DelayedTask stateMachine)
            => stateMachine.MoveNext();
    }

    private sealed class ImmediateTask<TArgs> : DelayedTask
    {
        internal ImmediateTask(Func<TArgs, CancellationToken, ValueTask> callback, TArgs args, CancellationToken token)
            : base(token)
            => Task = callback(args, this.token).AsTask();

        public override Task Task { get; }

        private protected override void SetException(Exception e) => Debug.Fail("Should not be called");

        private protected override void AdvanceStateMachine() => Debug.Fail("Should not be called");
    }

    private sealed class DelayedTaskStateMachine<TArgs> : DelayedTask
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
                    callbackAwaiter.GetResult();
                    builder.SetResult();
                    Cleanup();
                    break;
            }
        }

        private protected override void Cleanup()
        {
            callbackAwaiter = default;
            delayAwaiter = default;
            base.Cleanup();
        }

        private protected override void SetException(Exception e) => builder.SetException(e);
    }

    /// <summary>
    /// Represents a task with delayed completion.
    /// </summary>
    /// <typeparam name="TResult">The type of the result produced by this task.</typeparam>
    public abstract class DelayedTask<TResult> : DelayedTask
    {
        private protected DelayedTask(CancellationToken token)
            : base(token)
        {
        }

        /// <inheritdoc />
        public override abstract Task<TResult> Task { get; }

        /// <summary>
        /// Gets an awaiter used to await this task.
        /// </summary>
        /// <returns>An awaiter instance.</returns>
        public new TaskAwaiter<TResult> GetAwaiter() => Task.GetAwaiter();

        /// <summary>
        /// Configures an awaiter used to await this task.
        /// </summary>
        /// <param name="continueOnCapturedContext">
        /// <see langword="true"/> to attempt to marshal the continuation back to the original context captured;
        /// otherwise, <see langword="false"/>.
        /// </param>
        /// <returns>An awaiter instance.</returns>
        public new ConfiguredTaskAwaitable<TResult> ConfigureAwait(bool continueOnCapturedContext)
            => Task.ConfigureAwait(continueOnCapturedContext);
    }

    private sealed class ImmediateTask<TArgs, TResult> : DelayedTask<TResult>
    {
        internal ImmediateTask(Func<TArgs, CancellationToken, ValueTask<TResult>> callback, TArgs args, CancellationToken token)
            : base(token)
            => Task = callback(args, this.token).AsTask();

        public override Task<TResult> Task { get; }

        private protected override void SetException(Exception e) => Debug.Fail("Should not be called");

        private protected override void AdvanceStateMachine() => Debug.Fail("Should not be called");
    }

    private sealed class DelayedTaskStateMachine<TArgs, TResult> : DelayedTask<TResult>
    {
        private readonly Func<TArgs, CancellationToken, ValueTask<TResult>> callback;
        private readonly TArgs args;
        private readonly TimeSpan delay;
        private AsyncTaskMethodBuilder<TResult> builder;
        private ConfiguredTaskAwaitable.ConfiguredTaskAwaiter delayAwaiter;
        private ConfiguredValueTaskAwaitable<TResult>.ConfiguredValueTaskAwaiter callbackAwaiter;

        private DelayedTaskStateMachine(Func<TArgs, CancellationToken, ValueTask<TResult>> callback, TArgs args, TimeSpan delay, CancellationToken token)
            : base(token)
        {
            Debug.Assert(callback is not null);

            this.callback = callback;
            this.delay = delay;
            this.args = args;
            builder = AsyncTaskMethodBuilder<TResult>.Create();
            GC.KeepAlive(builder.Task); // initialize promise task immediately
        }

        internal static DelayedTask<TResult> Start(Func<TArgs, CancellationToken, ValueTask<TResult>> callback, TArgs args, TimeSpan delay, CancellationToken token)
        {
            var machine = new DelayedTaskStateMachine<TArgs, TResult>(callback, args, delay, token);
            Start(machine);
            return machine;
        }

        public override Task<TResult> Task => builder.Task;

        private protected override void AdvanceStateMachine()
        {
            switch (state)
            {
                case InitialState:
                    delayAwaiter = System.Threading.Tasks.Task.Delay(delay, token).ConfigureAwait(false).GetAwaiter();
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
                    builder.SetResult(callbackAwaiter.GetResult());
                    Cleanup();
                    break;
            }
        }

        private protected override void Cleanup()
        {
            delayAwaiter = default;
            callbackAwaiter = default;
            base.Cleanup();
        }

        private protected override void SetException(Exception e) => builder.SetException(e);
    }

    /// <summary>
    /// Represents an exception indicating that the delayed task is canceled safely without entering
    /// the scheduled callback.
    /// </summary>
    public sealed class DelayedTaskCanceledException : OperationCanceledException
    {
        internal DelayedTaskCanceledException(OperationCanceledException e)
            : base(e.Message, e, e.CancellationToken)
        {
        }
    }
}