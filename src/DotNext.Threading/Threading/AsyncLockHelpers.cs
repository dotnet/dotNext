using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    internal static class AsyncLockHelpers
    {
        internal const TaskContinuationOptions CheckOnTimeoutOptions = TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnRanToCompletion;
        private static readonly Action<Task<bool>> CheckOnTimemout = task =>
        {
            if (!task.Result)
                throw new TimeoutException();
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SuppressMessage("Reliability", "CA2008", Justification = "Timeout check cannot cause deadlock so Current task scheduler is OK")]
        internal static Task CheckOnTimeout(this Task<bool> continuation) => continuation.ContinueWith(CheckOnTimemout, CheckOnTimeoutOptions);
    }
}