using Microsoft.Extensions.Logging;
using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IAuditTrail = IO.Log.IAuditTrail;
    using IRaftAuditTrail = IO.Log.IAuditTrail<IRaftLogEntry>;
    using IDistributedLockEngine = DistributedServices.IDistributedLockEngine;

    public partial class RaftCluster<TMember>
    {
        /// <summary>
        /// Requests cluster service.
        /// </summary>
        /// <param name="serviceType">Requested service type.</param>
        /// <returns>The cluster service; or <see langword="null"/> if service is not supported.</returns>
        public virtual object? GetService(Type serviceType)
        {
            if(serviceType.IsAssignableFrom(GetType()))
                return this;
            if(serviceType == typeof(ILogger))
                return Logger;
            if(serviceType.IsOneOf(typeof(IAuditTrail), typeof(IPersistentState), typeof(IRaftAuditTrail)))
                return AuditTrail;
            if(serviceType == typeof(IDistributedLockEngine))
                return AuditTrail as IDistributedLockEngine;
            return null;
        }
    }
}