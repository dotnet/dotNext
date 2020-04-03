using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    public abstract partial class RaftCluster<TMember> : IRaftRpcHandler
    {
        Task<bool> IRaftRpcHandler.ResignAsync(CancellationToken token)
            => ReceiveResignAsync(token);
        
        Task<Result<bool>> IRaftRpcHandler.ReceiveVoteAsync(long term, long lastLogIndex, long lastLogTerm, EndPoint sender, CancellationToken token)
        {
            var member = FindMember(sender.Represents);
            return member is null ? Task.FromResult(new Result<bool>(Term, false)) : ReceiveVoteAsync(member, term, lastLogIndex, lastLogTerm, token);
        }
    }
}