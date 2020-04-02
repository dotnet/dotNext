using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    internal interface IRaftRpcServer
    {
        Task<Result<bool>> VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

        IReadOnlyDictionary<string, string> Metadata { get; }
    }
}