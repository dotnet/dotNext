using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
        private protected readonly CancellationToken token; // cached token to avoid ObjectDisposedException
        private volatile CancellationTokenSource? tokenSource;

        private protected DelayedTask(CancellationToken token)
            => this.token = (tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token)).Token;

        /// <summary>
        /// Gets delayed task.
        /// </summary>
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

        private protected void Cleanup() => Interlocked.Exchange(ref tokenSource, null)?.Dispose();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected static void GetResultAndClear(ref ConfiguredTaskAwaitable.ConfiguredTaskAwaiter awaiter)
        {
            awaiter.GetResult();
            awaiter = default;
        }

        /// <summary>
        /// Gets delayed task.
        /// </summary>
        /// <param name="task">Delayed task.</param>
        /// <returns>The delayed task.</returns>
        [return: NotNullIfNotNull("task")]
        public static implicit operator Task?(DelayedTask? task) => task?.Task;
    }

    private sealed class DelayedTaskStateMachine<TArgs> : DelayedTask, IAsyncStateMachine
    {
        private readonly Func<TArgs, CancellationToken, ValueTask> callback;
        private readonly TArgs args;
        private readonly TimeSpan delay;
        private AsyncTaskMethodBuilder builder;
        private ConfiguredTaskAwaitable.ConfiguredTaskAwaiter delayAwaiter;
        private ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter callbackAwaiter;
        private byte state;

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

        private static void MoveNext(DelayedTaskStateMachine<TArgs> machine)
        {
            try
            {
                switch (machine.state)
                {
                    case 0:
                        machine.delayAwaiter = Task.Delay(machine.delay, machine.token).ConfigureAwait(false).GetAwaiter();
                        if (machine.delayAwaiter.IsCompleted)
                            goto case 1;
                        machine.state = 1;
                        machine.builder.AwaitOnCompleted(ref machine.delayAwaiter, ref machine);
                        break;
                    case 1:
                        GetResultAndClear(ref machine.delayAwaiter);
                        machine.callbackAwaiter = machine.callback.Invoke(machine.args, machine.token).ConfigureAwait(false).GetAwaiter();
                        if (machine.callbackAwaiter.IsCompleted)
                            goto default;
                        machine.state = 2;
                        machine.builder.AwaitOnCompleted(ref machine.callbackAwaiter, ref machine);
                        break;
                    default:
                        machine.callbackAwaiter.GetResult();
                        machine.builder.SetResult();
                        machine.Cleanup();
                        break;
                }
            }
            catch (Exception e)
            {
                machine.Cleanup();
                machine.builder.SetException(e);
            }
        }

        void IAsyncStateMachine.MoveNext() => MoveNext(this);

        private new void Cleanup()
        {
            callbackAwaiter = default;
            delayAwaiter = default;
            base.Cleanup();
        }

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

        /// <summary>
        /// Gets delayed task.
        /// </summary>
        public override abstract Task<TResult> Task { get; }

        /// <summary>
        /// Gets delayed task.
        /// </summary>
        /// <param name="task">Delayed task.</param>
        /// <returns>The delayed task.</returns>
        [return: NotNullIfNotNull("task")]
        public static implicit operator Task<TResult>?(DelayedTask<TResult>? task) => task?.Task;
    }

    private sealed class DelayedTaskStateMachine<TArgs, TResult> : DelayedTask<TResult>, IAsyncStateMachine
    {
        private readonly Func<TArgs, CancellationToken, ValueTask<TResult>> callback;
        private readonly TArgs args;
        private readonly TimeSpan delay;
        private AsyncTaskMethodBuilder<TResult> builder;
        private ConfiguredTaskAwaitable.ConfiguredTaskAwaiter delayAwaiter;
        private ConfiguredValueTaskAwaitable<TResult>.ConfiguredValueTaskAwaiter callbackAwaiter;
        private byte state;

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

        private static void MoveNext(DelayedTaskStateMachine<TArgs, TResult> machine)
        {
            try
            {
                switch (machine.state)
                {
                    case 0:
                        machine.delayAwaiter = System.Threading.Tasks.Task.Delay(machine.delay, machine.token).ConfigureAwait(false).GetAwaiter();
                        if (machine.delayAwaiter.IsCompleted)
                            goto case 1;
                        machine.state = 1;
                        machine.builder.AwaitOnCompleted(ref machine.delayAwaiter, ref machine);
                        break;
                    case 1:
                        GetResultAndClear(ref machine.delayAwaiter);
                        machine.callbackAwaiter = machine.callback.Invoke(machine.args, machine.token).ConfigureAwait(false).GetAwaiter();
                        if (machine.callbackAwaiter.IsCompleted)
                            goto default;
                        machine.state = 2;
                        machine.builder.AwaitOnCompleted(ref machine.callbackAwaiter, ref machine);
                        break;
                    default:
                        machine.builder.SetResult(machine.callbackAwaiter.GetResult());
                        machine.Cleanup();
                        break;
                }
            }
            catch (Exception e)
            {
                machine.Cleanup();
                machine.builder.SetException(e);
            }
        }

        void IAsyncStateMachine.MoveNext() => MoveNext(this);

        private new void Cleanup()
        {
            delayAwaiter = default;
            callbackAwaiter = default;
            base.Cleanup();
        }

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
            => builder.SetStateMachine(stateMachine);
    }
}