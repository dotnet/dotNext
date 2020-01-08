using System;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using DistributedServices;
    using Threading;

    internal partial class RaftHttpCluster : IDistributedApplicationEnvironment
    {
        //distributed services
        private IDistributedLockProvider? distributedLock;

        IDistributedLockProvider IDistributedApplicationEnvironment.LockProvider => distributedLock ?? throw new NotSupportedException(ExceptionMessages.DistributedServicesAreUnavailable);

        private void InitializeDistributedServices(RaftClusterMember localMember)
        {
            distributedLock = TryCreateLockProvider(this, localMember);
        }
    }
}
