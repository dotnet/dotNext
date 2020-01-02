using System;

namespace DotNext.IO.Log
{
    internal interface IDistributedApplicationState : IAuditTrail
    {
        ref readonly Guid NodeId { get; }
    }
}