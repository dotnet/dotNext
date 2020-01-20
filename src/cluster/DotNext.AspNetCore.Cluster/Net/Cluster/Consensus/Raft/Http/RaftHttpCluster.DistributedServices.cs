using System;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using DistributedServices;
    using Messaging;
    using Threading;

    internal partial class RaftHttpCluster : IDistributedApplicationEnvironment
    {
        //distributed services
        private IDistributedLockProvider? distributedLock;

        IDistributedLockProvider IDistributedApplicationEnvironment.LockProvider => distributedLock ?? throw new NotSupportedException(ExceptionMessages.DistributedServicesAreUnavailable);

        private void ActivateDistributedLock(RaftClusterMember localMember)
        {
            var distributedLock = TryCreateLockProvider(this, localMember);
            if(distributedLock != null)
            {
                this.distributedLock = distributedLock;
                messageHandlers = messageHandlers.Insert(0, distributedLock);
            }
        }

        private void InitializeDistributedServices(RaftClusterMember localMember)
        {
            ActivateDistributedLock(localMember);
        }
    }
}
