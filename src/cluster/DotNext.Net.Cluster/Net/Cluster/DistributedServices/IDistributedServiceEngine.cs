using System;

namespace DotNext.Net.Cluster.DistributedServices
{
    using IAuditTrail = IO.Log.IAuditTrail;

    internal interface IDistributedServiceEngine : IAuditTrail
    {
        ref readonly Guid NodeId { get; }
    }
}