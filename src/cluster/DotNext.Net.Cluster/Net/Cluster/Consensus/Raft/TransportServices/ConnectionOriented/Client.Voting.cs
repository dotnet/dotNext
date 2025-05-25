using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using static Buffers.ByteBuffer;

internal partial class Client
{
    [StructLayout(LayoutKind.Auto)]
    private readonly struct VoteExchange(long term, long lastLogIndex, long lastLogTerm) : IClientExchange<Result<bool>>, IClientExchange<Result<PreVoteResult>>
    {
        ValueTask IClientExchange<Result<bool>>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            var writer = protocol.BeginRequestMessage(MessageType.Vote);
            writer.Write<PreVoteMessage>(new(localMember.Id, term, lastLogIndex, lastLogTerm));
            protocol.AdvanceWriteCursor(writer.WrittenCount);
            return protocol.WriteToTransportAsync(token);
        }

        static ValueTask<Result<bool>> IClientExchange<Result<bool>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadBoolResultAsync(token);

        static string IClientExchange<Result<bool>>.Name => "Vote";

        ValueTask IClientExchange<Result<PreVoteResult>>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            var writer = protocol.BeginRequestMessage(MessageType.PreVote);
            writer.Write<PreVoteMessage>(new(localMember.Id, term, lastLogIndex, lastLogTerm));
            protocol.AdvanceWriteCursor(writer.WrittenCount);
            return protocol.WriteToTransportAsync(token);
        }

        static ValueTask<Result<PreVoteResult>> IClientExchange<Result<PreVoteResult>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadPreVoteResultAsync(token);

        static string IClientExchange<Result<PreVoteResult>>.Name => "PreVote";
    }

    private protected sealed override Task<Result<bool>> VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => RequestAsync<Result<bool>, VoteExchange>(new(term, lastLogIndex, lastLogTerm), token);

    private protected sealed override Task<Result<PreVoteResult>> PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => RequestAsync<Result<PreVoteResult>, VoteExchange>(new(term, lastLogIndex, lastLogTerm), token);
}