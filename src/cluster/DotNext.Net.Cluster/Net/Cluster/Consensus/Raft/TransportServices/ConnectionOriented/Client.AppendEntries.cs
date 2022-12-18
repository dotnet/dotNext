using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using System;
using System.Threading;
using System.Threading.Tasks;
using IClusterConfiguration = Membership.IClusterConfiguration;

internal partial class Client : RaftClusterMember
{
    private sealed class AppendEntriesRequest<TEntry, TList> : Request<Result<bool>>
        where TEntry : notnull, IRaftLogEntry
        where TList : notnull, IReadOnlyList<TEntry>
    {
        private readonly ILocalMember localMember;
        private readonly long term, prevLogIndex, prevLogTerm, commitIndex;
        private readonly TList entries;
        private readonly IClusterConfiguration config;
        private readonly bool applyConfig;

        internal AppendEntriesRequest(ILocalMember localMember, long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig)
        {
            Debug.Assert(localMember is not null);
            Debug.Assert(config is not null);

            this.localMember = localMember;
            this.term = term;
            this.entries = entries;
            this.prevLogIndex = prevLogIndex;
            this.prevLogTerm = prevLogTerm;
            this.commitIndex = commitIndex;
            this.config = config;
            this.applyConfig = applyConfig;
        }

        private protected override ValueTask RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WriteAppendEntriesRequestAsync<TEntry, TList>(localMember.Id, term, entries, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig, buffer, token);

        private protected override ValueTask<Result<bool>> ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadResultAsync(token);
    }

    private protected sealed override Task<Result<bool>> AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
        => RequestAsync(new AppendEntriesRequest<TEntry, TList>(localMember, term, entries, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig), token);
}