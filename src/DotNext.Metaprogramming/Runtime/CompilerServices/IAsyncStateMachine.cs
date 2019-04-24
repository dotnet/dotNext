using System;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// This interface here for design purposes only
    /// to ensure that state machine class is written correctly.
    /// </summary>
    /// <typeparam name="STATE">Type of internal state.</typeparam>
    internal interface IAsyncStateMachine<STATE> : IAsyncStateMachine
    {
        STATE State { get; }
        uint StateId { get; }
        bool MoveNext<TAwaiter>(ref TAwaiter awaiter, uint stateId)
            where TAwaiter : INotifyCompletion;
        void Rethrow();
        bool HasNoException { get; }
        void EnterGuardedCode(uint newState);
        void ExitGuardedCode(uint previousState);
        bool TryRecover<E>(out E exception)
            where E : Exception;
    }
}