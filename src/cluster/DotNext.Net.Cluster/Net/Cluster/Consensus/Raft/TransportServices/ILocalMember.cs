using Microsoft.Extensions.Logging;
using NullLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using IO.Log;

internal interface ILocalMember
{
    IReadOnlyDictionary<string, string> Metadata { get; }

    ref readonly ClusterMemberId Id { get; }

    bool IsLeader(IRaftClusterMember member);

    ValueTask ProposeConfigurationAsync(Func<Memory<byte>, CancellationToken, ValueTask> configurationReader, long configurationLength, long fingerprint, CancellationToken token);

    ValueTask<Result<HeartbeatResult>> AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, long? fingerprint, bool applyConfig, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry;

    ValueTask<Result<HeartbeatResult>> AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, Membership.IClusterConfiguration config, bool applyConfig, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry;

    ValueTask<Result<bool>> VoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

    ValueTask<Result<PreVoteResult>> PreVoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

    ValueTask<bool> ResignAsync(CancellationToken token);

    ValueTask<Result<HeartbeatResult>> InstallSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
        where TSnapshot : notnull, IRaftLogEntry;

    ValueTask<long?> SynchronizeAsync(long commitIndex, CancellationToken token);

    ILogger Logger => NullLogger.Instance;
}