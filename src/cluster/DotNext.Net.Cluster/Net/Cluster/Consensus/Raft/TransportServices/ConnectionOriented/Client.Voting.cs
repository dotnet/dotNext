using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

internal partial class Client : RaftClusterMember
{
    private abstract class VoteRequestBase<TResult> : Request<Result<TResult>>
    {
        private protected readonly ILocalMember localMember;
        private protected readonly long term, lastLogIndex, lastLogTerm;

        internal VoteRequestBase(ILocalMember localMember, long term, long lastLogIndex, long lastLogTerm)
        {
            Debug.Assert(localMember is not null);

            this.localMember = localMember;
            this.term = term;
            this.lastLogIndex = lastLogIndex;
            this.lastLogTerm = lastLogTerm;
        }
    }

    // TODO: Change to required init properties in C# 11
    private sealed class VoteRequest : VoteRequestBase<bool>
    {
        internal VoteRequest(ILocalMember localMember, long term, long lastLogIndex, long lastLogTerm)
            : base(localMember, term, lastLogIndex, lastLogTerm)
        {
        }

        private protected override ValueTask RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WriteVoteRequestAsync(in localMember.Id, term, lastLogIndex, lastLogTerm, token);

        private protected override ValueTask<Result<bool>> ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadResultAsync(token);
    }

    private sealed class PreVoteRequest : VoteRequestBase<PreVoteResult>
    {
        internal PreVoteRequest(ILocalMember localMember, long term, long lastLogIndex, long lastLogTerm)
            : base(localMember, term, lastLogIndex, lastLogTerm)
        {
        }

        private protected override ValueTask RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WritePreVoteRequestAsync(in localMember.Id, term, lastLogIndex, lastLogTerm, token);

        private protected override ValueTask<Result<PreVoteResult>> ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadPreVoteResultAsync(token);
    }

    private protected sealed override Task<Result<bool>> VoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => RequestAsync(new VoteRequest(localMember, term, lastLogIndex, lastLogTerm), token);

    private protected sealed override Task<Result<PreVoteResult>> PreVoteAsync(long term, long lastLogIndex, long lastLogTerm, CancellationToken token)
        => RequestAsync(new PreVoteRequest(localMember, term, lastLogIndex, lastLogTerm), token);
}