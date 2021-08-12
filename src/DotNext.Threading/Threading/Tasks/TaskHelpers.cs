using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    internal static class AsyncLockHelpers
    {
        private static readonly Action<Task<bool>> CheckOnTimeoutAction = CheckOnTimeoutImpl;

        private static void CheckOnTimeoutImpl(Task<bool> task)
        {
            if (!task.Result)
                throw new TimeoutException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Task CheckOnTimeout(this Task<bool> continuation)
        {
            Task result;
            switch (continuation.Status)
            {
                default:
                    result = continuation.ContinueWith(CheckOnTimeoutAction, TaskContinuationOptions.ExecuteSynchronously);
                    break;
                case TaskStatus.RanToCompletion:
                    CheckOnTimeoutImpl(continuation);
                    goto case TaskStatus.Faulted;
                case TaskStatus.Faulted:
                case TaskStatus.Canceled:
                    result = continuation;
                    break;
            }

            return result;
        }
    }
}