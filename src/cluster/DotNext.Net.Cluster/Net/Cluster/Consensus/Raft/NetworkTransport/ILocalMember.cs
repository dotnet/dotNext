using Microsoft.Extensions.Logging;
using NullLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger;

namespace DotNext.Net.Cluster.Consensus.Raft.NetworkTransport;

using IO;
using IO.Log;

internal interface ILocalMember
{
    IReadOnlyDictionary<string, string> Metadata { get; }

    ref readonly ClusterMemberId Id { get; }

    bool IsLeader(IRaftClusterMember member);

    ValueTask<Result<HeartbeatResult>> AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, int stateVersion, CancellationToken token)
        where TEntry : IRaftLogEntry;

    ValueTask<Result<bool>> VoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, int stateVersion, CancellationToken token);

    ValueTask<Result<PreVoteResult>> PreVoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, int stateVersion, CancellationToken token);

    ValueTask<bool> ResignAsync(CancellationToken token);

    ValueTask<Result<HeartbeatResult>> InstallSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot,
        long snapshotIndex, int stateVersion, CancellationToken token)
        where TSnapshot : IRaftLogEntry;

    ValueTask<bool> InstallConfigurationAsync<TConfiguration>(long senderTerm, TConfiguration configuration,
        long configurationVersion, CancellationToken token)
        where TConfiguration : IDataTransferObject;

    ValueTask<long?> SynchronizeAsync(long commitIndex, CancellationToken token);

    ILogger Logger => NullLogger.Instance;
    
    int Version { get; }
}