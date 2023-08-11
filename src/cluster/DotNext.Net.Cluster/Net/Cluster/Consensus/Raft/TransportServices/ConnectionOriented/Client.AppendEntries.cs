using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using IClusterConfiguration = Membership.IClusterConfiguration;

internal partial class Client : RaftClusterMember
{
    // optimized version for empty heartbeats (it has no field to store empty entries)
    [StructLayout(LayoutKind.Auto)]
    [RequiresPreviewFeatures]
    private readonly struct AppendEntriesExchange : IClientExchange<Result<HeartbeatResult>>
    {
        internal const string Name = "AppendEntries";

        private readonly long term, prevLogIndex, prevLogTerm, commitIndex;
        private readonly IClusterConfiguration config;
        private readonly bool applyConfig;

        internal AppendEntriesExchange(long term, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig)
        {
            Debug.Assert(config is not null);

            this.term = term;
            this.prevLogIndex = prevLogIndex;
            this.prevLogTerm = prevLogTerm;
            this.commitIndex = commitIndex;
            this.config = config;
            this.applyConfig = applyConfig;
        }

        ValueTask IClientExchange<Result<HeartbeatResult>>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WriteAppendEntriesRequestAsync<EmptyLogEntry, EmptyLogEntry[]>(localMember.Id, term, Array.Empty<EmptyLogEntry>(), prevLogIndex, prevLogTerm, commitIndex, config, applyConfig, buffer, token);

        static ValueTask<Result<HeartbeatResult>> IClientExchange<Result<HeartbeatResult>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadHeartbeatResult(token);

        static string IClientExchange<Result<HeartbeatResult>>.Name => Name;
    }

    [StructLayout(LayoutKind.Auto)]
    [RequiresPreviewFeatures]
    private readonly struct AppendEntriesExchange<TEntry, TList> : IClientExchange<Result<HeartbeatResult>>
        where TEntry : notnull, IRaftLogEntry
        where TList : notnull, IReadOnlyList<TEntry>
    {
        private readonly long term, prevLogIndex, prevLogTerm, commitIndex;
        private readonly TList entries;
        private readonly IClusterConfiguration config;
        private readonly bool applyConfig;

        internal AppendEntriesExchange(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig)
        {
            Debug.Assert(config is not null);

            this.term = term;
            this.entries = entries;
            this.prevLogIndex = prevLogIndex;
            this.prevLogTerm = prevLogTerm;
            this.commitIndex = commitIndex;
            this.config = config;
            this.applyConfig = applyConfig;
        }

        ValueTask IClientExchange<Result<HeartbeatResult>>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.WriteAppendEntriesRequestAsync<TEntry, TList>(localMember.Id, term, entries, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig, buffer, token);

        static ValueTask<Result<HeartbeatResult>> IClientExchange<Result<HeartbeatResult>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadHeartbeatResult(token);

        static string IClientExchange<Result<HeartbeatResult>>.Name => AppendEntriesExchange.Name;
    }

    [RequiresPreviewFeatures]
    private protected sealed override Task<Result<HeartbeatResult>> AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
    {
        return entries.Count > 0
            ? RequestAsync<Result<HeartbeatResult>, AppendEntriesExchange<TEntry, TList>>(new(term, entries, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig), token)
            : RequestAsync<Result<HeartbeatResult>, AppendEntriesExchange>(new(term, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig), token);
    }
}