using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NullLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using IO.Log;

    internal interface ILocalMember
    {
        IReadOnlyDictionary<string, string> Metadata { get; }

        IPEndPoint Address { get; }

        bool IsLeader(IRaftClusterMember member);

        Task<Result<bool>> AppendEntriesAsync<TEntry>(EndPoint sender, long senderTerm, ILogEntryProducer<TEntry> entries, long prevLogIndex, long prevLogTerm, long commitIndex, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry;

        Task<Result<bool>> VoteAsync(EndPoint sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

        Task<Result<bool>> PreVoteAsync(EndPoint sender, long term, long lastLogIndex, long lastLogTerm, CancellationToken token);

        Task<bool> ResignAsync(CancellationToken token);

        Task<Result<bool>> InstallSnapshotAsync<TSnapshot>(EndPoint sender, long senderTerm, TSnapshot snapshot, long snapshotIndex, CancellationToken token)
            where TSnapshot : notnull, IRaftLogEntry;

        ILogger Logger => NullLogger.Instance;
    }
}