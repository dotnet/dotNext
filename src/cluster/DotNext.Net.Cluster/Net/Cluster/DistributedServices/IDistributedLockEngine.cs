using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.DistributedServices
{
    using Threading;
    
    using DistributedLockInfo = Threading.DistributedLockInfo;

    /// <summary>
    /// Represents engine of distributed services.
    /// </summary>
    internal interface IDistributedLockEngine : IDistributedServiceEngine
    {
        Task RestoreAsync(CancellationToken token);

        AsyncEventListener CreateReleaseLockListener(CancellationToken token);

        AsyncEventListener CreateAcquireLockListener(CancellationToken token);

        bool IsAcquired(string lockName, Guid version);

        //releases all expired locks
        Task CollectGarbage(CancellationToken token);

        //writes the log entry describing lock acquisition
        //but doesn't wait for commit    
        Task<bool> PrepareAcquisitionAsync(string name, DistributedLockInfo lockInfo, CancellationToken token);
    }
}