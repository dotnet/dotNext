using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// This interface here for design purposes only
    /// to ensure that state machine class is written correctly.
    /// </summary>
    /// <typeparam name="TState">Type of internal state.</typeparam>
    internal interface IAsyncStateMachine<out TState> : IAsyncStateMachine
    {
        /// <summary>
        /// Represents final state identifier of async state machine.
        /// </summary>
        internal const uint FinalState = 0U;

        TState State { get; }

        uint StateId { get; }

        bool MoveNext<TAwaiter>(ref TAwaiter awaiter, uint stateId)
            where TAwaiter : INotifyCompletion;

        void Rethrow();

        bool HasNoException { get; }

        void EnterGuardedCode(uint newState);

        void ExitGuardedCode(uint previousState, bool suspendException);

        bool TryRecover<TException>([NotNullWhen(true)] out TException? exception)
            where TException : Exception;
    }

    /// <summary>
    /// Represents body of async method in the form of state machine transitions.
    /// </summary>
    /// <typeparam name="TState">The type of async method state.</typeparam>
    /// <typeparam name="TMachine">The implementation of async state machine.</typeparam>
    /// <param name="stateMachine">Asyncronous state machine.</param>
    internal delegate void Transition<out TState, TMachine>(ref TMachine stateMachine)
        where TMachine : struct, IAsyncStateMachine<TState>;
}