using System;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Provides manual control over asynchronous state machine.
    /// </summary>
    /// <remarks>
    /// This type allows to implement custom async/await flow
    /// and intended for expert-level developers.
    /// </remarks>
    public struct AsyncStateMachine<STATE>: IAsyncStateMachine
    {
        /// <summary>
        /// State machine transition.
        /// </summary>
        /// <param name="stateMachine">State machine to modify during transition.</param>
        public delegate void Transition(ref AsyncStateMachine<STATE> stateMachine);

        internal const int FINAL_STATE = int.MinValue;
        public const int INITIAL_STATE = 0;

        /// <summary>
        /// Runtime state associated with this state machine.
        /// </summary>
        public STATE State;

        private AsyncTaskMethodBuilder builder;
        private readonly Transition transition;

        public AsyncStateMachine(Transition transition, STATE state)
        {
            builder = AsyncTaskMethodBuilder.Create();
            this.transition = transition;
            this.State = state;
            StateId = INITIAL_STATE;
        }

        public int StateId
        {
            get;
            private set;
        }

        /// <summary>
        /// Transition into final state with exception.
        /// </summary>
        public Exception Exception
        {
            set
            {
                StateId = FINAL_STATE;
                builder.SetException(value);
            }
        }

        void IAsyncStateMachine.MoveNext()
        {
            try
            {
                transition(ref this);
            }
            catch(Exception e)
            {
                Exception = e;
            }
        }

        /// <summary>
        /// Performs transition.
        /// </summary>
        /// <typeparam name="TAwaiter">Type of asynchronous control flow object.</typeparam>
        /// <param name="awaiter">Asynchronous result obtained from another method to await.</param>
        /// <param name="stateId">A new state identifier.</param>
        public void MoveNext<TAwaiter>(ref TAwaiter awaiter, int stateId)
            where TAwaiter: ICriticalNotifyCompletion
        {
            StateId = stateId;
            builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);
        }

        public void Complete()
        {
            StateId = FINAL_STATE;
            builder.SetResult();
        }
        
        public void Start()
        {
            StateId = INITIAL_STATE;
            builder.Start(ref this);
        }

        public static implicit operator Task(in AsyncStateMachine<STATE> machine) => machine.builder.Task;

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine) => builder.SetStateMachine(stateMachine);
    }

    public struct AsyncStateMachine<STATE, R>: IAsyncStateMachine
        where STATE: struct
    {
        public delegate void Transition(ref AsyncStateMachine<STATE, R> stateMachine);

        public STATE State;
        private AsyncTaskMethodBuilder<R> builder;
        private readonly Transition transition;

        public AsyncStateMachine(Transition transition, STATE state)
        {
            builder = AsyncTaskMethodBuilder<R>.Create();
            StateId = AsyncStateMachine<STATE>.INITIAL_STATE;
            State = state;
            this.transition = transition;
        }

        public int StateId
        {
            get;
            private set;
        }

        public void Start()
        {
            StateId = AsyncStateMachine<STATE>.INITIAL_STATE;
            builder.Start(ref this);
        }

        public Exception Exception
        {
            set
            {
                StateId = AsyncStateMachine<STATE>.FINAL_STATE;
                builder.SetException(value);
            }
        }

        public R Result
        {
            set
            {
                StateId = AsyncStateMachine<STATE>.FINAL_STATE;
                builder.SetResult(value);
            }
        }

        public void MoveNext<TAwaiter>(ref TAwaiter awaiter, int stateId)
            where TAwaiter: ICriticalNotifyCompletion
        {
            StateId = stateId;
            builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);
        }

        void IAsyncStateMachine.MoveNext()
        {
            try
            {
                transition(ref this);
            }
            catch(Exception e)
            {
                Exception = e;
            }
        }

        public static implicit operator Task<R>(in AsyncStateMachine<STATE, R> machine) => machine.builder.Task;

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine) => builder.SetStateMachine(stateMachine);
    }
}