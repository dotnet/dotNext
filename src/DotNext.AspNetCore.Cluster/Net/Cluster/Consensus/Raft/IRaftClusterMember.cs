using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal interface IRaftClusterMember : IClusterMember, IDisposable
    {
         Task<bool?> Vote(Guid sender, CancellationToken token);

         void CancelPendingRequests();
    }
}