using System;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices
{
    public interface IAsyncStateMachine<STATE> : IAsyncStateMachine
    {
        STATE State { get; }
        int StateId { get; }
        Exception Exception { set; }
        void MoveNext<TAwaiter>(ref TAwaiter awaiter, int stateId)
            where TAwaiter : ICriticalNotifyCompletion;
    }
}