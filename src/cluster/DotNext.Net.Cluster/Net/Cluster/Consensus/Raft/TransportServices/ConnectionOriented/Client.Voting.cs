using System.Runtime.Versioning;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;

internal partial class Client : RaftClusterMember
{
    [RequiresPreviewFeatures]
    private sealed class VoteExchange : IClientExchange<Result<bool>>, IClientExchange<Result<PreVoteResult>>
    {
        private readonly long term, lastLogIndex, lastLogTerm;

        internal VoteExchange(long term, long lastLogIndex, long lastLogTerm)
        {
            this.term = term;
            this.lastLogIndex = lastLogIndex;
            this.lastLogTerm = lastLogTerm;
        }

        ValueTask IClientExchange<Result<bool>>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            var writer = protocol.BeginRequestMessage(MessageType.Vote);
            VoteMessage.Write(ref writer, in localMember.Id, term, lastLogIndex, lastLogTerm);
            protocol.AdvanceWriteCursor(writer.WrittenCount);
            return protocol.WriteToTransportAsync(token);
        }

        static ValueTask<Result<bool>> IClientExchange<Result<bool>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadBoolResultAsync(token);

        static string IClientExchange<Result<bool>>.Name => "Vote";

        ValueTask IClientExchange<Result<PreVoteResult>>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            var writer = protocol.BeginRequestMessage(MessageType.PreVote);
            PreVoteMessage.Write(ref writer, in localMember.Id, term, lastLogIndex, lastLogTerm);
            protocol.AdvanceWriteCursor(writer.WrittenCount);
            return protocol.WriteToTransportAsync(token);
        }

        static ValueTask<Result<PreVoteResult>> IClientExchange<Result<PreVoteResult>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadPreVoteResultAsync(token);

        static string IClientExchange<Result<PreVoteResult>>.Name => "PreVote";
    }

    [RequiresPreviewFeatures]
    private protected sealed override Task<Result<bool>> VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => RequestAsync<Result<bool>, VoteExchange>(new(term, lastLogIndex, lastLogTerm), token);

    [RequiresPreviewFeatures]
    private protected sealed override Task<Result<PreVoteResult>> PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => RequestAsync<Result<PreVoteResult>, VoteExchange>(new(term, lastLogIndex, lastLogTerm), token);
}