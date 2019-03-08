using System;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.ConstrainedExecution;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Provides manual control over asynchronous state machine.
    /// </summary>
    /// <remarks>
    /// This type allows to implement custom async/await flow
    /// and intended for expert-level developers.
    /// </remarks>
    /// <typeparam name="STATE">The local state of async function used to store computation state.</typeparam>
    [CLSCompliant(false)]
    public struct AsyncStateMachine<STATE>: IAsyncStateMachine<STATE>
    {
        /// <summary>
        /// Represents state-transition function.
        /// </summary>
        /// <param name="stateMachine">A state to modify during transition.</param>
        public delegate void Transition(ref AsyncStateMachine<STATE> stateMachine);

        /// <summary>
        /// Represents final state identifier of async state machine.
        /// </summary>
        public const uint FINAL_STATE = 0;

        /// <summary>
        /// Runtime state associated with this state machine.
        /// </summary>
        public STATE State;

        private AsyncTaskMethodBuilder builder;
        private readonly Transition transition;
        private ExceptionDispatchInfo exception;
        private ushort guardedRegionsCounter;    //number of entries into try-clause

        private AsyncStateMachine(Transition transition, STATE state)
        {
            builder = AsyncTaskMethodBuilder.Create();
            this.transition = transition;
            State = state;
            StateId = FINAL_STATE;
            exception = null;
            guardedRegionsCounter = 0;
        }

        STATE IAsyncStateMachine<STATE>.State => State;

        /// <summary>
        /// Gets state identifier.
        /// </summary>
        public uint StateId
        {
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            get;
            private set;
        }

        /// <summary>
        /// Enters guarded code block which represents <see langword="try"/> block of code
        /// inside of async lambda function.
        /// </summary>
        /// <param name="newState">The identifier of the async state machine representing guared code.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public void EnterGuardedCode(uint newState)
        {
            StateId = newState;
            guardedRegionsCounter += 1;
        }

        /// <summary>
        /// Leaves guarded code block.
        /// </summary>
        /// <param name="previousState">The identifier of the async state machine before invocation of <see cref="EnterGuardedCode(uint)"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public void ExitGuardedCode(uint previousState)
        {
            StateId = previousState;
            guardedRegionsCounter -= 1;
            
        }

        /// <summary>
        /// Attempts to recover from the exception and indicating prologue of <see langword="catch"/> statement
        /// inside of async lambda function.
        /// </summary>
        /// <typeparam name="E">Type of expression to be caught.</typeparam>
        /// <param name="restoredException">Reference to the captured exception.</param>
        /// <returns><see langword="true"/>, if caught exception is of type <typeparamref name="E"/>; otherwise, <see langword="false"/>.</returns>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public bool TryRecover<E>(out E restoredException)
            where E : Exception
        {
            if (exception?.SourceException is E typed)
            {
                exception = null;
                restoredException = typed;
                return true;
            }
            else
            {
                restoredException = null;
                return false;
            }
        }

        /// <summary>
        /// Indicates that this async state machine is not in exceptional state.
        /// </summary>
        public bool HasNoException
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            get => exception is null;
        }

        /// <summary>
        /// Re-throws capture exception.
        /// </summary>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public void Rethrow() => exception?.Throw();

        void IAsyncStateMachine.MoveNext()
        {
        begin:
            try
            {
                transition(ref this);
            }
            catch (Exception e)
            {
                exception = ExceptionDispatchInfo.Capture(e);
                //try to recover from exception and re-enter into state machine
                if (guardedRegionsCounter > 0)
                    goto begin;
            }
            //finalize state machine
            if (StateId == FINAL_STATE)
            {
                if (exception is null)
                    builder.SetResult();
                else
                    builder.SetException(exception.SourceException);
                guardedRegionsCounter = 0;
                exception = null;
            }
        }

        /// <summary>
        /// Performs transition.
        /// </summary>
        /// <typeparam name="TAwaiter">Type of asynchronous control flow object.</typeparam>
        /// <param name="awaiter">Asynchronous result obtained from another method to await.</param>
        /// <param name="stateId">A new state identifier.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveNext<TAwaiter>(ref TAwaiter awaiter, uint stateId)
            where TAwaiter: ICriticalNotifyCompletion
        {
            StateId = stateId;
            builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);
        }

        /// <summary>
        /// Turns this state machine into final state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public void Complete()
        {
            StateId = FINAL_STATE;
            exception = null;
        }

        private Task Start()
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
        public static Task Start(Transition transition, STATE initialState = default)
            => new AsyncStateMachine<STATE>(transition, initialState).Start();

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine) => builder.SetStateMachine(stateMachine);
    }

    /// <summary>
    /// Provides manual control over asynchronous state machine.
    /// </summary>
    /// <remarks>
    /// This type allows to implement custom async/await flow
    /// and intended for expert-level developers.
    /// </remarks>
    /// <typeparam name="STATE">The local state of async function used to store computation state.</typeparam>
    /// <typeparam name="R">Result type of asynchronous function.</typeparam>
    [CLSCompliant(false)]
    public struct AsyncStateMachine<STATE, R> : IAsyncStateMachine<STATE>
        where STATE : struct
    {
        /// <summary>
        /// Represents state-transition function.
        /// </summary>
        /// <param name="stateMachine"></param>
        public delegate void Transition(ref AsyncStateMachine<STATE, R> stateMachine);

        /// <summary>
        /// Represents internal state.
        /// </summary>
        public STATE State;
        private AsyncTaskMethodBuilder<R> builder;
        private readonly Transition transition;
        private ExceptionDispatchInfo exception;
        private ushort guardedRegionsCounter;    //number of entries into try-clause
        private R result;

        private AsyncStateMachine(Transition transition, STATE state)
        {
            builder = AsyncTaskMethodBuilder<R>.Create();
            StateId = AsyncStateMachine<STATE>.FINAL_STATE;
            State = state;
            this.transition = transition;
            guardedRegionsCounter = 0;
            exception = null;
            result = default;
        }

        STATE IAsyncStateMachine<STATE>.State => State;

        /// <summary>
        /// Gets state identifier.
        /// </summary>
        public uint StateId
        {
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            get;
            private set;
        }

        /// <summary>
        /// Enters guarded code block which represents <see langword="try"/> block of code
        /// inside of async lambda function.
        /// </summary>
        /// <param name="newState">The identifier of the async machine state representing guared code.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public void EnterGuardedCode(uint newState)
        {
            StateId = newState;
            guardedRegionsCounter += 1;
        }

        /// <summary>
        /// Leaves guarded code block.
        /// </summary>
        /// <param name="previousState">The identifier of the async state machine before invocation of <see cref="EnterGuardedCode(uint)"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public void ExitGuardedCode(uint previousState)
        {
            StateId = previousState;
            guardedRegionsCounter -= 1;
        }

        /// <summary>
        /// Attempts to recover from the exception and indicating prologue of <see langword="catch"/> statement
        /// inside of async lambda function.
        /// </summary>
        /// <typeparam name="E">Type of expression to be caught.</typeparam>
        /// <param name="restoredException">Reference to the captured exception.</param>
        /// <returns><see langword="true"/>, if caught exception is of type <typeparamref name="E"/>; otherwise, <see langword="false"/>.</returns>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public bool TryRecover<E>(out E restoredException)
            where E : Exception
        {
            if (exception?.SourceException is E typed)
            {
                exception = null;
                restoredException = typed;
                return true;
            }
            else
            {
                restoredException = null;
                return false;
            }
        }

        /// <summary>
        /// Indicates that this async state machine is not in exceptional state.
        /// </summary>
        public bool HasNoException
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            get => exception is null;
        }

        /// <summary>
        /// Re-throws capture exception.
        /// </summary>
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        public void Rethrow() => exception?.Throw();
        
        private Task<R> Start()
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
        public static Task<R> Start(Transition transition, STATE initialState = default)
            => new AsyncStateMachine<STATE, R>(transition, initialState).Start();

        /// <summary>
        /// Sets result of async state machine and marks current state as final state.
        /// </summary>
        public R Result
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            set
            {
                StateId = AsyncStateMachine<STATE>.FINAL_STATE;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MoveNext<TAwaiter>(ref TAwaiter awaiter, uint stateId)
            where TAwaiter : ICriticalNotifyCompletion
        {
            StateId = stateId;
            builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);
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
                exception = ExceptionDispatchInfo.Capture(e);
                //try to recover from exception and re-enter into state machine
                if (guardedRegionsCounter > 0)
                    goto begin;
            }
            //finalize state machine
            if (StateId == AsyncStateMachine<STATE>.FINAL_STATE)
            {
                if (exception is null)
                    builder.SetResult(result);
                else
                    builder.SetException(exception.SourceException);
                guardedRegionsCounter = 0;
                exception = null;
            }
        }

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine) => builder.SetStateMachine(stateMachine);
    }
}