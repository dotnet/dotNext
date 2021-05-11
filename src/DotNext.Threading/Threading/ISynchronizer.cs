using System;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    using Timestamp = Diagnostics.Timestamp;

    /// <summary>
    /// Common interface for all synchronization primitives.
    /// </summary>
    public interface ISynchronizer
    {
        internal class WaitNode : TaskCompletionSource<bool>
        {
            private readonly Timestamp current;

            internal WaitNode()
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
                current = Timestamp.Current;
            }

            internal void SetResult() => SetResult(true);

            internal TimeSpan Age => current.Elapsed;
        }

        /// <summary>
        /// Indicates that there is one or more suspended callers.
        /// </summary>
        bool HasAnticipants { get; }
    }
}