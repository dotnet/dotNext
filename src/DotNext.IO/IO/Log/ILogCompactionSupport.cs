using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO.Log
{
    /// <summary>
    /// Represents audit trail that supports background compaction
    /// of committed log entries.
    /// </summary>
    public interface ILogCompactionSupport : IAuditTrail
    {
        /// <summary>
        /// Forces compaction of committed log entries.
        /// </summary>
        /// <param name="token">Tje token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask ForceCompactionAsync(CancellationToken token);
    }
}