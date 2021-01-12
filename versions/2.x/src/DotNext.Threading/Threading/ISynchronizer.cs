using System.Threading.Tasks;

namespace DotNext.Threading
{
    /// <summary>
    /// Common interface for all synchronization primitives.
    /// </summary>
    public interface ISynchronizer
    {
        internal class WaitNode : TaskCompletionSource<bool>
        {
            internal WaitNode()
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
            }

            internal void Complete() => SetResult(true);
        }

        /// <summary>
        /// Indicates that one or more asynchronous callers are suspended.
        /// </summary>
        bool HasWaiters { get; }
    }
}