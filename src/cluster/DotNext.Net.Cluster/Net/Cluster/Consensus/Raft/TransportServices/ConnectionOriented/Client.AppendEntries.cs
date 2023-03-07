using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using IClusterConfiguration = Membership.IClusterConfiguration;

internal partial class Client : RaftClusterMember
{
    [StructLayout(LayoutKind.Auto)]
    [RequiresPreviewFeatures]
    private readonly struct AppendEntriesExchange<TEntry, TList> : IClientExchange<Result<bool>>
        where TEntry : notnull, IRaftLogEntry
        where TList : notnull, IReadOnlyList<TEntry>
    {
        private const string Name = "AppendEntries";

        private readonly ILocalMember localMember;
        private readonly long term, prevLogIndex, prevLogTerm, commitIndex;
        private readonly TList entries;
        private readonly IClusterConfiguration config;
        private readonly bool applyConfig;

        internal AppendEntriesExchange(ILocalMember localMember, long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig)
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

        ValueTask IClientExchange<Result<bool>>.RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WriteAppendEntriesRequestAsync<TEntry, TList>(localMember.Id, term, entries, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig, buffer, token);

        static ValueTask<Result<bool>> IClientExchange<Result<bool>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadResultAsync(token);

        static string IClientExchange<Result<bool>>.Name => Name;
    }

    [RequiresPreviewFeatures]
    private protected sealed override Task<Result<bool>> AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
        => RequestAsync<Result<bool>, AppendEntriesExchange<TEntry, TList>>(new(localMember, term, entries, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig), token);
}