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
        int StateId { get; }
        void MoveNext<TAwaiter>(ref TAwaiter awaiter, int stateId)
            where TAwaiter : ICriticalNotifyCompletion;
        Exception CapturedException { get; }
        void Rethrow();
        void EnterGuardedCode(int newState);
        void ExitGuardedCode();
    }
}