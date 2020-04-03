using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal interface IRaftRpcHandler
    {
        Task<Result<bool>> ReceiveVoteAsync(long term, long lastLogIndex, long lastLogTerm, EndPoint sender, CancellationToken token);

        Task<bool> ResignAsync(CancellationToken token);
    }

    internal interface ILocalMember : IRaftRpcHandler
    {
        IReadOnlyDictionary<string, string> Metadata { get; }
    }
}