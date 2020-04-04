using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    public abstract partial class RaftCluster<TMember> : IRaftRpcHandler
    {
        Task<bool> IRaftRpcHandler.ResignAsync(CancellationToken token)
            => ReceiveResignAsync(token);
        
        Task<Result<bool>> IRaftRpcHandler.ReceiveEntriesAsync<TEntry>(EndPoint sender, long senderTerm, IO.Log.ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
        {
            var member = FindMember(sender.Represents);
            return member is null ? Task.FromResult(new Result<bool>(Term, false)) : ReceiveEntriesAsync(member, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, token);
        }
        
        Task<Result<bool>> IRaftRpcHandler.ReceiveVoteAsync(EndPoint sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        {
            var member = FindMember(sender.Represents);
            return member is null ? Task.FromResult(new Result<bool>(Term, false)) : ReceiveVoteAsync(member, term, lastLogIndex, lastLogTerm, token);
        }
    }
}