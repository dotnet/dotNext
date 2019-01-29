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
    public struct AsyncStateMachine<STATE>: IAsyncStateMachine<STATE>
    {
        /// <summary>
        /// Represents state-transition function.
        /// </summary>
        /// <param name="stateMachine">A state to modify during transition.</param>
        public delegate void Transition(ref AsyncStateMachine<STATE> stateMachine);

        public const int FINAL_STATE = 0;

        /// <summary>
        /// Runtime state associated with this state machine.
        /// </summary>
        public STATE State;

        private AsyncTaskMethodBuilder builder;
        private readonly Transition transition;
        private ExceptionDispatchInfo exception;
        private ushort guardedRegionCounter;    //number of entries into try-clause

        private AsyncStateMachine(Transition transition, STATE state)
        {
            builder = AsyncTaskMethodBuilder.Create();
            this.transition = transition;
            State = state;
            StateId = FINAL_STATE;
            exception = null;
            guardedRegionCounter = 0;
        }

        STATE IAsyncStateMachine<STATE>.State => State;

        /// <summary>
        /// Gets state identifier.
        /// </summary>
        public int StateId
        {
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            get;
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            private set;
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public void EnterGuardedCode(int newState)
        {
            StateId = newState;
            guardedRegionCounter += 1;
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public void ExitGuardedCode() => guardedRegionCounter -= 1;

        /// <summary>
        /// Gets captured exception.
        /// </summary>
        public Exception CapturedException => exception?.SourceException;

        /// <summary>
        /// Re-throws capture exception.
        /// </summary>
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
                if (guardedRegionCounter > 0)
                {
                    guardedRegionCounter -= 1;
                    goto begin;
                }
            }
            //finalize state machine
            if (StateId == FINAL_STATE)
            {
                if (exception is null)
                    builder.SetResult();
                else
                    builder.SetException(exception.SourceException);
                builder = default;
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
        public void MoveNext<TAwaiter>(ref TAwaiter awaiter, int stateId)
            where TAwaiter: ICriticalNotifyCompletion
        {
            StateId = stateId;
            builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public void Complete() => StateId = FINAL_STATE;

        public static Task Start(Transition transition, STATE initialState = default)
        {
            var stateMachine = new AsyncStateMachine<STATE>(transition, initialState);
            stateMachine.builder.Start(ref stateMachine);
            return stateMachine.builder.Task;
        }

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine) => builder.SetStateMachine(stateMachine);
    }

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
        private ushort guardedRegionCounter;    //number of entries into try-clause
        private R result;

        private AsyncStateMachine(Transition transition, STATE state)
        {
            builder = AsyncTaskMethodBuilder<R>.Create();
            StateId = AsyncStateMachine<STATE>.FINAL_STATE;
            State = state;
            this.transition = transition;
            guardedRegionCounter = 0;
            exception = null;
            result = default;
        }

        STATE IAsyncStateMachine<STATE>.State => State;

        public int StateId
        {
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            get;
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            private set;
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public void EnterGuardedCode(int newState)
        {
            StateId = newState;
            guardedRegionCounter += 1;
        }

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        public void ExitGuardedCode() => guardedRegionCounter -= 1;

        /// <summary>
        /// Gets captured exception.
        /// </summary>
        public Exception CapturedException => exception?.SourceException;

        /// <summary>
        /// Re-throws capture exception.
        /// </summary>
        public void Rethrow() => exception?.Throw();

        public static Task<R> Start(Transition transition, STATE initialState = default)
        {
            var stateMachine = new AsyncStateMachine<STATE, R>(transition, initialState);
            stateMachine.builder.Start(ref stateMachine);
            return stateMachine.builder.Task;
        }
        
        /// <summary>
        /// Sets result of async state machine and marks current state as final state.
        /// </summary>
        public R Result
        {
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            set
            {
                StateId = AsyncStateMachine<STATE>.FINAL_STATE;
                result = value;
            }
        }

        public void MoveNext<TAwaiter>(ref TAwaiter awaiter, int stateId)
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
                if (guardedRegionCounter > 0)
                {
                    guardedRegionCounter -= 1;
                    goto begin;
                }
            }
            //finalize state machine
            if (StateId == AsyncStateMachine<STATE>.FINAL_STATE)
            {
                if (exception is null)
                    builder.SetResult(result);
                else
                    builder.SetException(exception.SourceException);
                builder = default;
                exception = null;
            }
        }

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine) => builder.SetStateMachine(stateMachine);
    }
}