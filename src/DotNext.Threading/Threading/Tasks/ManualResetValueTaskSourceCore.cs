using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

namespace DotNext.Threading.Tasks
{
    internal static class ManualResetValueTaskSourceCore
    {
        // cached to avoid allocations
        private static readonly Action<object?> Continuation = Invoke!;

        private static void Invoke(object state)
            => Unsafe.As<Action>(state).Invoke();

        internal static void OnCompleted<TResult>(this ref ManualResetValueTaskSourceCore<TResult> source, Action callback, short token, ValueTaskSourceOnCompletedFlags flags)
            => source.OnCompleted(Continuation, callback, token, flags);
    }
}