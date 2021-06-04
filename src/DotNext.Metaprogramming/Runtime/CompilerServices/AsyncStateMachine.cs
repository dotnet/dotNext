using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Provides manual control over asynchronous state machine.
    /// </summary>
    /// <remarks>
    /// This type allows to implement custom async/await flow
    /// and intended for expert-level developers.
    /// </remarks>
    /// <typeparam name="TState">The local state of async function used to store computation state.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    internal struct AsyncStateMachine<TState> : IAsyncStateMachine<TState>
        where TState : struct
    {
        private readonly Transition<TState, AsyncStateMachine<TState>> transition;

        /// <summary>
        /// Runtime state associated with this state machine.
        /// </summary>
        public TState State;
        private AsyncValueTaskMethodBuilder builder;
        private ExceptionDispatchInfo? exception;
        private bool suspended;
        private uint guardedRegionsCounter;    // number of entries into try-clause

        private AsyncStateMachine(Transition<TState, AsyncStateMachine<TState>> transition, TState state)
        {
            builder = AsyncValueTaskMethodBuilder.Create();
            this.transition = transition;
            State = state;
            StateId = IAsyncStateMachine<TState>.FinalState;
            exception = null;
            suspended = false;
            guardedRegionsCounter = 0;
            suspended = false;
        }

        readonly TState IAsyncStateMachine<TState>.State => State;

        /// <summary>
        /// Gets state identifier.
        /// </summary>
        public uint StateId
        {
            readonly get;
            private set;
        }

        /// <summary>
        /// Enters guarded code block which represents <c>try</c> block of code
        /// inside of async lambda function.
        /// </summary>
        /// <param name="newState">The identifier of the async state machine representing guarded code.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterGuardedCode(uint newState)
        {
            StateId = newState;
            guardedRegionsCounter += 1;
        }

        /// <summary>
        /// Leaves guarded code block.
        /// </summary>
        /// <param name="previousState">The identifier of the async state machine before invocation of <see cref="EnterGuardedCode(uint)"/>.</param>
        /// <param name="suspendException"><see langword="true"/> to suspend exception then entering finally block; otherwise, <see langword="false"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitGuardedCode(uint previousState, bool suspendException)
        {
            StateId = previousState;
            guardedRegionsCounter -= 1;
            suspended = exception is not null && suspendException;
        }

        /// <summary>
        /// Attempts to recover from the exception and indicating prologue of <c>try</c> statement
        /// inside of async lambda function.
        /// </summary>
        /// <typeparam name="TException">Type of expression to be caught.</typeparam>
        /// <param name="restoredException">Reference to the captured exception.</param>
        /// <returns><see langword="true"/>, if caught exception is of type <typeparamref name="TException"/>; otherwise, <see langword="false"/>.</returns>
        public bool TryRecover<TException>([NotNullWhen(true)] out TException? restoredException)
            where TException : Exception
        {
            if (exception?.SourceException is TException typed)
            {
                exception = null;
                restoredException = typed;
                return true;
            }

            restoredException = null;
            return false;
        }

        /// <summary>
        /// Indicates that this async state machine is not in exceptional state.
        /// </summary>
        public readonly bool HasNoException
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => exception is null || suspended;
        }

        /// <summary>
        /// Re-throws captured exception.
        /// </summary>
        public void Rethrow()
        {
            suspended = false;
            exception?.Throw();
        }

        void IAsyncStateMachine.MoveNext()
        {
        begin:
            try
            {
                transition(ref this);
            }
            catch (Exception e)
            {
                suspended = false;
                exception = ExceptionDispatchInfo.Capture(e);

                // try to recover from exception and re-enter into state machine
                if (guardedRegionsCounter > 0)
                    goto begin;

                // no exception handlers - just finalize state machine
                StateId = IAsyncStateMachine<TState>.FinalState;
            }

            // finalize state machine
            if (StateId == IAsyncStateMachine<TState>.FinalState)
            {
                if (exception is null)
                    builder.SetResult();
                else
                    builder.SetException(exception.SourceException);

                // perform cleanup after resuming of all suspended tasks
                guardedRegionsCounter = 0;
                exception = null;
                State = default;
            }
        }

        /// <summary>
        /// Performs transition.
        /// </summary>
        /// <typeparam name="TAwaiter">Type of asynchronous control flow object.</typeparam>
        /// <param name="awaiter">Asynchronous result obtained from another method to await.</param>
        /// <param name="stateId">A new state identifier.</param>
        /// <returns><see langword="true"/> if awaiter is completed synchronously; otherwise, <see langword="false"/>.</returns>
        public bool MoveNext<TAwaiter>(ref TAwaiter awaiter, uint stateId)
            where TAwaiter : INotifyCompletion
        {
            StateId = stateId;

            // avoid boxing of this state machine through continuation action if awaiter is completed already
            if (Awaiter<TAwaiter>.IsCompleted(ref awaiter))
                return true;

            builder.AwaitOnCompleted(ref awaiter, ref this);
            return false;
        }

        /// <summary>
        /// Turns this state machine into final state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Complete() => StateId = IAsyncStateMachine<TState>.FinalState;

        private ValueTask Start()
        {
            builder.Start(ref this);
            return builder.Task;
        }

        /// <summary>
        /// Executes async state machine.
        /// </summary>
        /// <param name="transition">Async function which execution is controlled by state machine.</param>
        /// <param name="initialState">Initial state.</param>
        /// <returns>The task representing execution of async function.</returns>
        public static ValueTask Start(Transition<TState, AsyncStateMachine<TState>> transition, TState initialState = default)
            => new AsyncStateMachine<TState>(transition, initialState).Start();

        readonly void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine) => builder.SetStateMachine(stateMachine);
    }

    /// <summary>
    /// Provides manual control over asynchronous state machine.
    /// </summary>
    /// <remarks>
    /// This type allows to implement custom async/await flow
    /// and intended for expert-level developers.
    /// </remarks>
    /// <typeparam name="TState">The local state of async function used to store computation state.</typeparam>
    /// <typeparam name="TResult">Result type of asynchronous function.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    internal struct AsyncStateMachine<TState, TResult> : IAsyncStateMachine<TState>
        where TState : struct
    {
        private readonly Transition<TState, AsyncStateMachine<TState, TResult>> transition;

        /// <summary>
        /// Represents internal state.
        /// </summary>
        public TState State;
        private AsyncValueTaskMethodBuilder<TResult?> builder;
        private ExceptionDispatchInfo? exception;
        private bool suspended;
        private uint guardedRegionsCounter;    // number of entries into try-clause
        private TResult? result;

        private AsyncStateMachine(Transition<TState, AsyncStateMachine<TState, TResult>> transition, TState state)
        {
            builder = AsyncValueTaskMethodBuilder<TResult?>.Create();
            StateId = IAsyncStateMachine<TState>.FinalState;
            State = state;
            this.transition = transition;
            suspended = false;
            guardedRegionsCounter = 0;
            exception = null;
            result = default;
        }

        readonly TState IAsyncStateMachine<TState>.State => State;

        /// <summary>
        /// Gets state identifier.
        /// </summary>
        public uint StateId
        {
            readonly get;
            private set;
        }

        /// <summary>
        /// Enters guarded code block which represents <c>try</c> block of code
        /// inside of async lambda function.
        /// </summary>
        /// <param name="newState">The identifier of the async machine state representing guarded code.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterGuardedCode(uint newState)
        {
            StateId = newState;
            guardedRegionsCounter += 1;
        }

        /// <summary>
        /// Leaves guarded code block.
        /// </summary>
        /// <param name="previousState">The identifier of the async state machine before invocation of <see cref="EnterGuardedCode(uint)"/>.</param>
        /// <param name="suspendException"><see langword="true"/> to suspend exception then entering finally block; otherwise, <see langword="false"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitGuardedCode(uint previousState, bool suspendException)
        {
            StateId = previousState;
            guardedRegionsCounter -= 1;
            suspended = exception is not null && suspendException;
        }

        /// <summary>
        /// Attempts to recover from the exception and indicating prologue of <c>try</c> statement
        /// inside of async lambda function.
        /// </summary>
        /// <typeparam name="TException">Type of expression to be caught.</typeparam>
        /// <param name="restoredException">Reference to the captured exception.</param>
        /// <returns><see langword="true"/>, if caught exception is of type <typeparamref name="TException"/>; otherwise, <see langword="false"/>.</returns>
        public bool TryRecover<TException>([NotNullWhen(true)] out TException? restoredException)
            where TException : Exception
        {
            if (exception?.SourceException is TException typed)
            {
                exception = null;
                restoredException = typed;
                return true;
            }

            restoredException = null;
            return false;
        }

        /// <summary>
        /// Indicates that this async state machine is not in exceptional state.
        /// </summary>
        public readonly bool HasNoException
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => exception is null || suspended;
        }

        /// <summary>
        /// Re-throws captured exception.
        /// </summary>
        public void Rethrow()
        {
            suspended = false;
            exception?.Throw();
        }

        private ValueTask<TResult?> Start()
        {
            builder.Start(ref this);
            return builder.Task;
        }

        /// <summary>
        /// Executes async state machine.
        /// </summary>
        /// <param name="transition">Async function which execution is controlled by state machine.</param>
        /// <param name="initialState">Initial state.</param>
        /// <returns>The task representing execution of async function.</returns>
        public static ValueTask<TResult?> Start(Transition<TState, AsyncStateMachine<TState, TResult>> transition, TState initialState = default)
            => new AsyncStateMachine<TState, TResult>(transition, initialState).Start();

        /// <summary>
        /// Sets result of async state machine and marks current state as final state.
        /// </summary>
        public TResult Result
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                StateId = IAsyncStateMachine<TState>.FinalState;
                exception = null;
                result = value;
            }
        }

        /// <summary>
        /// Performs transition.
        /// </summary>
        /// <typeparam name="TAwaiter">Type of asynchronous control flow object.</typeparam>
        /// <param name="awaiter">Asynchronous result obtained from another method to await.</param>
        /// <param name="stateId">A new state identifier.</param>
        /// <returns><see langword="true"/> if awaiter is completed synchronously; otherwise, <see langword="false"/>.</returns>
        public bool MoveNext<TAwaiter>(ref TAwaiter awaiter, uint stateId)
            where TAwaiter : INotifyCompletion
        {
            StateId = stateId;

            // avoid boxing of this state machine through continuation action if awaiter is completed already
            if (Awaiter<TAwaiter>.IsCompleted(ref awaiter))
                return true;

            builder.AwaitOnCompleted(ref awaiter, ref this);
            return false;
        }

        void IAsyncStateMachine.MoveNext()
        {
        begin:
            try
            {
                transition(ref this);
            }
            catch (Exception e)
            {
                suspended = false;
                exception = ExceptionDispatchInfo.Capture(e);

                // try to recover from exception and re-enter into state machine
                if (guardedRegionsCounter > 0)
                    goto begin;

                // no exception handlers - just finalize state machine
                StateId = IAsyncStateMachine<TState>.FinalState;
            }

            // finalize state machine
            if (StateId == IAsyncStateMachine<TState>.FinalState)
            {
                if (exception is null)
                    builder.SetResult(result);
                else
                    builder.SetException(exception.SourceException);

                // perform cleanup after resuming of all suspended tasks
                guardedRegionsCounter = 0;
                exception = null;
                result = default;
                State = default;
            }
        }

        readonly void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine) => builder.SetStateMachine(stateMachine);
    }
}