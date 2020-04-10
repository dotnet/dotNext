using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;

    public abstract partial class RaftCluster<TMember> : IRaftRpcHandler
    {
        Task<bool> IRaftRpcHandler.ResignAsync(CancellationToken token)
            => ReceiveResignAsync(token);
        
        Task<Result<bool>> IRaftRpcHandler.ReceiveEntriesAsync<TEntry>(EndPoint sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
        {
            var member = FindMember(sender.Represents);
            return member is null ? Task.FromResult(new Result<bool>(Term, false)) : ReceiveEntriesAsync(member, senderTerm, entries, prevLogIndex, prevLogTerm, commitIndex, token);
        }
        
        Task<Result<bool>> IRaftRpcHandler.ReceiveVoteAsync(EndPoint sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        {
            var member = FindMember(sender.Represents);
            return member is null ? Task.FromResult(new Result<bool>(Term, false)) : ReceiveVoteAsync(member, term, lastLogIndex, lastLogTerm, token);
        }

        Task<Result<bool>> IRaftRpcHandler.ReceiveSnapshotAsync<TSnapshot>(EndPoint sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
        {
            var member = FindMember(sender.Represents);
            return member is null ? Task.FromResult(new Result<bool>(Term, false)) : ReceiveSnapshotAsync(member, senderTerm, snapshot, snapshotIndex, token);
        }
    }
}