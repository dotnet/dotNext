using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

internal partial class Client : RaftClusterMember
{
    // TODO: Change to required init properties in C# 11
    [StructLayout(LayoutKind.Auto)]
    private readonly struct VoteExchange : IClientExchange<Result<bool>>, IClientExchange<Result<PreVoteResult>>
    {
        private readonly ILocalMember localMember;
        private readonly long term, lastLogIndex, lastLogTerm;

        internal VoteExchange(ILocalMember localMember, long term, long lastLogIndex, long lastLogTerm)
        {
            Debug.Assert(localMember is not null);

            this.localMember = localMember;
            this.term = term;
            this.lastLogIndex = lastLogIndex;
            this.lastLogTerm = lastLogTerm;
        }

        ValueTask IClientExchange<Result<bool>>.RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WriteVoteRequestAsync(in localMember.Id, term, lastLogIndex, lastLogTerm, token);

        ValueTask<Result<bool>> IClientExchange<Result<bool>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadResultAsync(token);

        ValueTask IClientExchange<Result<PreVoteResult>>.RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WritePreVoteRequestAsync(in localMember.Id, term, lastLogIndex, lastLogTerm, token);

        ValueTask<Result<PreVoteResult>> IClientExchange<Result<PreVoteResult>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadPreVoteResultAsync(token);
    }

    private protected sealed override Task<Result<bool>> VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => RequestAsync<VoteExchange, Result<bool>>(new(localMember, term, lastLogIndex, lastLogTerm), token);

    private protected sealed override Task<Result<PreVoteResult>> PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => RequestAsync<VoteExchange, Result<PreVoteResult>>(new(localMember, term, lastLogIndex, lastLogTerm), token);
}