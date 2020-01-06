using System;

namespace DotNext.IO.Log
{
    /// <summary>
    /// Represents state of distributed application.
    /// </summary>
    public interface IDistributedApplicationState : IAuditTrail
    {
        /// <summary>
        /// Gets persistent identifier of the application.
        /// </summary>
        ref readonly Guid NodeId { get; }
    }
}