using Microsoft.Extensions.Logging;
using NullLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using IO.Log;

internal interface ILocalMember
{
    IReadOnlyDictionary<string, string> Metadata { get; }

    ref readonly ClusterMemberId Id { get; }

    bool IsLeader(IRaftClusterMember member);

    Task ProposeConfigurationAsync(Func<Memory<byte>, CancellationToken, ValueTask> configurationReader, long configurationLength, long fingerprint, CancellationToken token);

    Task<Result<bool>> AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, long? fingerprint, bool applyConfig, CancellationToken token)
        where TEntry : notnull, IRaftLogEntry;

    Task<Result<bool>> VoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

    Task<Result<bool>> PreVoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

    Task<bool> ResignAsync(CancellationToken token);

    Task<Result<bool>> InstallSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
        where TSnapshot : notnull, IRaftLogEntry;

    Task<long?> SynchronizeAsync(CancellationToken token);

    ILogger Logger => NullLogger.Instance;
}