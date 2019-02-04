using System;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Provides manual control over asynchronous state machine.
    /// </summary>
    /// <remarks>
    /// This type allows to implement custom async/await flow
    /// and intended for expert-level developers.
    /// </remarks>
    [CLSCompliant(false)]
    public struct AsyncStateMachine<STATE>: IAsyncStateMachine<STATE>
    {
        /// <summary>
        /// Represents state-transition function.
        /// </summary>
        /// <param name="stateMachine">A state to modify during transition.</param>
        public delegate void Transition(ref AsyncStateMachine<STATE> stateMachine);

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
            get;
            private set;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterGuardedCode(uint newState)
        {
            StateId = newState;
            guardedRegionsCounter += 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitGuardedCode(uint previousState)
        {
            StateId = previousState;
            guardedRegionsCounter -= 1;
            
        }

        public bool TryRecover<E>(uint recoveryState, out E restoredException)
            where E : Exception
        {
            var exception = this.exception?.SourceException;
            if (exception is E typed)
            {
                this.exception = null;
                restoredException = typed;
                StateId = recoveryState;
                return true;
            }
            else
            {
                restoredException = null;
                return false;
            }
        }

        public bool HasNoException
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => exception is null;
        }

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
                builder = default;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Complete()
        {
            StateId = FINAL_STATE;
            exception = null;
        }

        private Task Start()
        {
            var result = builder.Task;
            builder.Start(ref this);
            return result;
        }

        public static Task Start(Transition transition, STATE initialState = default)
            => new AsyncStateMachine<STATE>(transition, initialState).Start();

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine) => builder.SetStateMachine(stateMachine);
    }

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

        public uint StateId
        {
            get;
            private set;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterGuardedCode(uint newState)
        {
            StateId = newState;
            guardedRegionsCounter += 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitGuardedCode(uint previousState)
        {
            StateId = previousState;
            guardedRegionsCounter -= 1;
        }

        public bool TryRecover<E>(uint recoveryState, out E restoredException)
            where E : Exception
        {
            var exception = this.exception?.SourceException;
            if (exception is E typed)
            {
                this.exception = null;
                restoredException = typed;
                StateId = recoveryState;
                return true;
            }
            else
            {
                restoredException = null;
                return false;
            }
        }

        public bool HasNoException
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => exception is null;
        }

        /// <summary>
        /// Re-throws capture exception.
        /// </summary>
        public void Rethrow() => exception?.Throw();
        
        private Task<R> Start()
        {
            var result = builder.Task;
            builder.Start(ref this);
            return result;
        }

        public static Task<R> Start(Transition transition, STATE initialState = default)
            => new AsyncStateMachine<STATE, R>(transition, initialState).Start();

        /// <summary>
        /// Sets result of async state machine and marks current state as final state.
        /// </summary>
        public R Result
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                StateId = AsyncStateMachine<STATE>.FINAL_STATE;
                exception = null;
                result = value;
            }
        }

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
                builder = default;
                exception = null;
            }
        }

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine) => builder.SetStateMachine(stateMachine);
    }
}