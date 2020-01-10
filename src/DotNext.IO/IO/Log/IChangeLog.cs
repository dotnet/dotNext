using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO.Log
{
    /// <summary>
    /// Represents incremental log of changes that can be used to reconstruct original dataset.
    /// </summary>
    public interface IChangeLog : IAuditTrail
    {
        /// <summary>
        /// Ensures that all committed entries are applied to the underlying state machine known as database engine.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>A task representing state of the asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
        Task EnsureConsistencyAsync(CancellationToken token = default);

        /// <summary>
        /// Replays all committed entries to reconstruct original dataset.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>A task representing state of the asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been cancelled.</exception>
        Task ReplayAsync(CancellationToken token = default);
    }
}
