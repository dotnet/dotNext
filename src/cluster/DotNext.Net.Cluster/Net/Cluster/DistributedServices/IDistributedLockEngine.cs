using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.DistributedServices
{
    using IDistributedApplicationState = IO.Log.IDistributedApplicationState;

    /// <summary>
    /// Represents engine of distributed services.
    /// </summary>
    internal interface IDistributedLockEngine : IDistributedApplicationState
    {
        void ValidateName(string name);

        Task RestoreAsync(CancellationToken token);

        Task<bool> WaitForLockEventAsync(bool acquireEvent, TimeSpan timeout, CancellationToken token);

        bool IsRegistered(string lockName, Guid version);

        //releases all expired locks
        Task ProvideSponsorshipAsync(Sponsor<DistributedLock> sponsor, CancellationToken token);

        //writes the log entry describing lock acquisition
        //but doesn't wait for commit    
        Task<bool> RegisterAsync(string name, DistributedLock lockInfo, CancellationToken token);
        
        //write the log entry describing lock release
        //but doesn't wait for commit
        Task<bool> UnregisterAsync(string name, Guid owner, Guid version, CancellationToken token);

        //write the log entry describing lock release
        //but doesn't wait for commit
        Task UnregisterAsync(string name, CancellationToken token);
    }
}