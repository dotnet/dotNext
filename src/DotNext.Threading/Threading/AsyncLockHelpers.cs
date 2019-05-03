using System;
using System.Runtime.CompilerServices;
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
        internal static Task CheckOnTimeout(this Task<bool> continuation) => continuation.ContinueWith(CheckOnTimemout, CheckOnTimeoutOptions);
    }
}