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
            var cts = Interlocked.Exchange(ref tokenSource, null);
            if (cts is not null)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected static unsafe void MoveNext<T>(T stateMachine, delegate*<T, void> stateMachineAction)
            where T : DelayedTask
        {
            Debug.Assert(stateMachineAction != null);

            try
            {
                stateMachineAction(stateMachine);
            }
            catch (Exception e)
            {
                stateMachine.Cleanup();

                if (stateMachine.state is DelayState && e is OperationCanceledException canceledEx)
                {
                    e = new DelayedTaskCanceledException(canceledEx);

                    if (canceledEx.StackTrace is { Length: > 0 } stackTrace)
                        ExceptionDispatchInfo.SetRemoteStackTrace(e, stackTrace);
                    else
                        ExceptionDispatchInfo.SetCurrentStackTrace(e);
                }

                stateMachine.SetException(e);
            }
        }
    }

    private sealed class DelayedTaskStateMachine<TArgs> : DelayedTask, IAsyncStateMachine
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
        }

        internal static DelayedTask Start(Func<TArgs, CancellationToken, ValueTask> callback, TArgs args, TimeSpan delay, CancellationToken token)
        {
            var machine = new DelayedTaskStateMachine<TArgs>(callback, args, delay, token);
            machine.builder.Start(ref machine);
            return machine;
        }

        public override Task Task => builder.Task;

        void IAsyncStateMachine.MoveNext()
        {
            unsafe
            {
                MoveNext(this, &AdvanceStateMachine);
            }

            static void AdvanceStateMachine(DelayedTaskStateMachine<TArgs> machine)
            {
                switch (machine.state)
                {
                    case InitialState:
                        machine.delayAwaiter = Task.Delay(machine.delay, machine.token).ConfigureAwait(false).GetAwaiter();
                        machine.state = DelayState;
                        if (machine.delayAwaiter.IsCompleted)
                            goto case DelayState;
                        machine.builder.AwaitOnCompleted(ref machine.delayAwaiter, ref machine);
                        break;
                    case DelayState:
                        GetResultAndClear(ref machine.delayAwaiter);
                        machine.callbackAwaiter = machine.callback.Invoke(machine.args, machine.token).ConfigureAwait(false).GetAwaiter();
                        if (machine.callbackAwaiter.IsCompleted)
                            goto default;
                        machine.state = DelayState + 1U;
                        machine.builder.AwaitOnCompleted(ref machine.callbackAwaiter, ref machine);
                        break;
                    default:
                        machine.callbackAwaiter.GetResult();
                        machine.builder.SetResult();
                        machine.Cleanup();
                        break;
                }
            }
        }

        private protected override void Cleanup()
        {
            callbackAwaiter = default;
            delayAwaiter = default;
            base.Cleanup();
        }

        private protected override void SetException(Exception e) => builder.SetException(e);

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
            => builder.SetStateMachine(stateMachine);
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

    private sealed class DelayedTaskStateMachine<TArgs, TResult> : DelayedTask<TResult>, IAsyncStateMachine
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
        }

        internal static DelayedTask<TResult> Start(Func<TArgs, CancellationToken, ValueTask<TResult>> callback, TArgs args, TimeSpan delay, CancellationToken token)
        {
            var machine = new DelayedTaskStateMachine<TArgs, TResult>(callback, args, delay, token);
            machine.builder.Start(ref machine);
            return machine;
        }

        public override Task<TResult> Task => builder.Task;

        void IAsyncStateMachine.MoveNext()
        {
            unsafe
            {
                MoveNext(this, &AdvanceStateMachine);
            }

            static void AdvanceStateMachine(DelayedTaskStateMachine<TArgs, TResult> machine)
            {
                switch (machine.state)
                {
                    case InitialState:
                        machine.delayAwaiter = System.Threading.Tasks.Task.Delay(machine.delay, machine.token).ConfigureAwait(false).GetAwaiter();
                        machine.state = DelayState;
                        if (machine.delayAwaiter.IsCompleted)
                            goto case DelayState;
                        machine.builder.AwaitOnCompleted(ref machine.delayAwaiter, ref machine);
                        break;
                    case DelayState:
                        GetResultAndClear(ref machine.delayAwaiter);
                        machine.callbackAwaiter = machine.callback.Invoke(machine.args, machine.token).ConfigureAwait(false).GetAwaiter();
                        if (machine.callbackAwaiter.IsCompleted)
                            goto default;
                        machine.state = DelayState + 1U;
                        machine.builder.AwaitOnCompleted(ref machine.callbackAwaiter, ref machine);
                        break;
                    default:
                        machine.builder.SetResult(machine.callbackAwaiter.GetResult());
                        machine.Cleanup();
                        break;
                }
            }
        }

        private protected override void Cleanup()
        {
            delayAwaiter = default;
            callbackAwaiter = default;
            base.Cleanup();
        }

        private protected override void SetException(Exception e) => builder.SetException(e);

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
            => builder.SetStateMachine(stateMachine);
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