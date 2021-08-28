using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NullLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using IO.Log;
    using IClusterConfiguration = Membership.IClusterConfiguration;

    internal interface ILocalMember
    {
        IReadOnlyDictionary<string, string> Metadata { get; }

        ref readonly ClusterMemberId Id { get; }

        bool IsLeader(IRaftClusterMember member);

        Task InstallConfigurationAsync<TConfiguration>(TConfiguration configuration, bool applyConfig, CancellationToken token)
            where TConfiguration : notnull, IClusterConfiguration;

        Task<Result<bool>> AppendEntriesAsync<TEntry>(ClusterMemberId sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry;

        Task<Result<bool>> VoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

        Task<Result<bool>> PreVoteAsync(ClusterMemberId sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

        Task<bool> ResignAsync(CancellationToken token);

        Task<Result<bool>> InstallSnapshotAsync<TSnapshot>(ClusterMemberId sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
            where TSnapshot : notnull, IRaftLogEntry;

        ILogger Logger => NullLogger.Instance;
    }
}