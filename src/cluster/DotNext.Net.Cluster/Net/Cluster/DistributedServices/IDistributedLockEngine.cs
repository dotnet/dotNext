using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.DistributedServices
{
    using AsyncEventListener = Threading.AsyncEventListener;
    using IAuditTrail = IO.Log.IAuditTrail;

    /// <summary>
    /// Represents engine of distributed services.
    /// </summary>
    internal interface IDistributedLockEngine : IAuditTrail
    {
        Task RestoreAsync(CancellationToken token);

        AsyncEventListener CreateReleaseLockListener(CancellationToken token);

        Task<bool> WaitForAcquisitionAsync(string lockName, TimeSpan timeout, CancellationToken token);

        //releases all expired locks
        Task CollectGarbage(CancellationToken token);
    }
}