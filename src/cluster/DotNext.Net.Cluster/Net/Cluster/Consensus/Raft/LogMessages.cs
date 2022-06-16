using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    [ExcludeFromCodeCoverage]
    internal static partial class LogMessages
    {
        private const string EventIdPrefix = "DotNext.Net.Cluster";
        private const int EventIdOffset = 74000;

        [LoggerMessage(
            EventIdOffset,
            LogLevel.Debug,
            "Member is downgrading to follower state",
            EventName = EventIdPrefix + "." + nameof(DowngradingToFollowerState)
        )]
        public static partial void DowngradingToFollowerState(this ILogger logger);

        [LoggerMessage(
            EventIdOffset + 1,
            LogLevel.Debug,
            "Member is downgraded to follower state",
            EventName = EventIdPrefix + "." + nameof(DowngradedToFollowerState)
        )]
        public static partial void DowngradedToFollowerState(this ILogger logger);

        [LoggerMessage(
            EventIdOffset + 2,
            LogLevel.Information,
            "Transition to Candidate state started",
            EventName = EventIdPrefix + "." + nameof(TransitionToCandidateStateStarted)
        )]
        public static partial void TransitionToCandidateStateStarted(this ILogger logger);

        [LoggerMessage(
            EventIdOffset + 3,
            LogLevel.Information,
            "Transition to Candidate state completed",
            EventName = EventIdPrefix + "." + nameof(TransitionToCandidateStateCompleted)
        )]
        public static partial void TransitionToCandidateStateCompleted(this ILogger logger);

        [LoggerMessage(
            EventIdOffset + 4,
            LogLevel.Information,
            "Transition to Leader state started",
            EventName = EventIdPrefix + "." + nameof(TransitionToLeaderStateStarted)
        )]
        public static partial void TransitionToLeaderStateStarted(this ILogger logger);

        [LoggerMessage(
            EventIdOffset + 5,
            LogLevel.Information,
            "Transition to Leader state completed",
            EventName = EventIdPrefix + "." + nameof(TransitionToLeaderStateCompleted)
        )]
        public static partial void TransitionToLeaderStateCompleted(this ILogger logger);

        [LoggerMessage(
            EventIdOffset + 6,
            LogLevel.Debug,
            "Voting is started with timeout {ElectionTimeout}",
            EventName = EventIdPrefix + "." + nameof(VotingStarted)
        )]
        public static partial void VotingStarted(this ILogger logger, int electionTimeout);

        [LoggerMessage(
            EventIdOffset + 7,
            LogLevel.Debug,
            "Voting is completed. Total vote weight is {Quorum}",
            EventName = EventIdPrefix + "." + nameof(VotingCompleted)
        )]
        public static partial void VotingCompleted(this ILogger logger, int quorum);

        [LoggerMessage(
            EventIdOffset + 8,
            LogLevel.Debug,
            "Vote is granted by member {Member}",
            EventName = EventIdPrefix + "." + nameof(VoteGranted)
        )]
        public static partial void VoteGranted(this ILogger logger, EndPoint member);

        [LoggerMessage(
            EventIdOffset + 9,
            LogLevel.Debug,
            "Vote is rejected by member {Member}",
            EventName = EventIdPrefix + "." + nameof(VoteRejected)
        )]
        public static partial void VoteRejected(this ILogger logger, EndPoint member);

        [LoggerMessage(
            EventIdOffset + 10,
            LogLevel.Debug,
            "Cluster member {Member} is unavailable",
            EventName = EventIdPrefix + "." + nameof(MemberUnavailable)
        )]
        public static partial void MemberUnavailable(this ILogger logger, EndPoint member, Exception? e = null);

        [LoggerMessage(
            EventIdOffset + 11,
            LogLevel.Debug,
            "Election timeout is refreshed",
            EventName = EventIdPrefix + "." + nameof(TimeoutReset)
        )]
        public static partial void TimeoutReset(this ILogger logger);

        [LoggerMessage(
            EventIdOffset + 12,
            LogLevel.Debug,
            "Replication of member {Member} started from log index {EntryIndex}",
            EventName = EventIdPrefix + "." + nameof(ReplicationStarted)
        )]
        public static partial void ReplicationStarted(this ILogger logger, EndPoint member, long entryIndex);

        [LoggerMessage(
            EventIdOffset + 13,
            LogLevel.Debug,
            "Replication of {Member} contains {LogEntries} entries. Preceding entry has index {PrevLogIndex} and term {PrevLogTerm}",
            EventName = EventIdPrefix + "." + nameof(ReplicaSize)
        )]
        public static partial void ReplicaSize(this ILogger logger, EndPoint? member, int logEntries, long prevLogIndex, long prevLogTerm);

        [LoggerMessage(
            EventIdOffset + 14,
            LogLevel.Debug,
            "Member {Member} is replicated successfully starting from entry {EntryIndex}",
            EventName = EventIdPrefix + "." + nameof(ReplicationSuccessful)
        )]
        public static partial void ReplicationSuccessful(this ILogger logger, EndPoint member, long entryIndex);

        [LoggerMessage(
            EventIdOffset + 15,
            LogLevel.Debug,
            "Replication of {Member} is failed. Retry replication from entry {EntryIndex}",
            EventName = EventIdPrefix + "." + nameof(ReplicationFailed)
        )]
        public static partial void ReplicationFailed(this ILogger logger, EndPoint member, long entryIndex);

        [LoggerMessage(
            EventIdOffset + 16,
            LogLevel.Debug,
            "Unable to commit local changes. Quorum is {Quorum}, last committed entry is {CommitIndex}",
            EventName = EventIdPrefix + "." + nameof(CommitFailed)
        )]
        public static partial void CommitFailed(this ILogger logger, int quorum, long commitIndex);

        [LoggerMessage(
            EventIdOffset + 17,
            LogLevel.Debug,
            "All changes started from {EntryIndex} are committed. The number of committed entries is {LogEntries}",
            EventName = EventIdPrefix + "." + nameof(CommitSuccessful)
        )]
        public static partial void CommitSuccessful(this ILogger logger, long entryIndex, long logEntries);

        [LoggerMessage(
            EventIdOffset + 18,
            LogLevel.Debug,
            "Installing snapshot with {EntryIndex} index of the last included log entry",
            EventName = EventIdPrefix + "." + nameof(InstallingSnapshot)
        )]
        public static partial void InstallingSnapshot(this ILogger logger, long entryIndex);

        public const string SnapshotInstallationFailed = "Critical error detected while installing snapshot of audit trail";

        [LoggerMessage(
            EventIdOffset + 19,
            LogLevel.Error,
            "Datagram with id {PacketId} has dropped from remote endpoint {Member} because it cannot be dispatched to appropriate logical channel",
            EventName = EventIdPrefix + "." + nameof(PacketDropped)
        )]
        public static partial void PacketDropped(this ILogger logger, TransportServices.Datagram.CorrelationId packetId, EndPoint? member);

        [LoggerMessage(
            EventIdOffset + 20,
            LogLevel.Error,
            "Too many pallel requests",
            EventName = EventIdPrefix + "." + nameof(NotEnoughRequestHandlers)
        )]
        public static partial void NotEnoughRequestHandlers(this ILogger logger);

        [LoggerMessage(
            EventIdOffset + 21,
            LogLevel.Error,
            "Socket error {SockerError} occurred",
            EventName = EventIdPrefix + "." + nameof(SockerErrorOccurred)
        )]
        public static partial void SockerErrorOccurred(this ILogger logger, SocketError sockerError);

        [LoggerMessage(
            EventIdOffset + 22,
            LogLevel.Warning,
            "Timeout occurred while processing request from {RemoteEndPoint}",
            EventName = EventIdPrefix + "." + nameof(RequestTimedOut)
        )]
        public static partial void RequestTimedOut(this ILogger logger, EndPoint? remoteEndPoint, OperationCanceledException e);

        [LoggerMessage(
            EventIdOffset + 23,
            LogLevel.Debug,
            "Listening of incoming connections unexpectedly failed",
            EventName = EventIdPrefix + "." + nameof(SocketAcceptLoopTerminated)
        )]
        public static partial void SocketAcceptLoopTerminated(this ILogger logger, Exception e);

        [LoggerMessage(
            EventIdOffset + 24,
            LogLevel.Warning,
            "Graceful shutdown of TCP server failed. Timeout is {Timeout}",
            EventName = EventIdPrefix + "." + nameof(TcpGracefulShutdownFailed)
        )]
        public static partial void TcpGracefulShutdownFailed(this ILogger logger, int timeout);

        [LoggerMessage(
            EventIdOffset + 25,
            LogLevel.Warning,
            "Unable to cancel all pending outbound requests",
            EventName = EventIdPrefix + "." + nameof(FailedToCancelPendingRequests)
        )]
        public static partial void FailedToCancelPendingRequests(this ILogger logger, Exception e);

        [LoggerMessage(
            EventIdOffset + 26,
            LogLevel.Warning,
            "StopAsync() method wasn't called",
            EventName = EventIdPrefix + "." + nameof(StopAsyncWasNotCalled)
        )]
        public static partial void StopAsyncWasNotCalled(this ILogger logger);

        [LoggerMessage(
            EventIdOffset + 27,
            LogLevel.Error,
            "TLS handshake with {RemoteEndPoint} failed",
            EventName = EventIdPrefix + "." + nameof(TlsHandshakeFailed)
        )]
        public static partial void TlsHandshakeFailed(this ILogger logger, EndPoint? remoteEndPoint, Exception e);

        [LoggerMessage(
            EventIdOffset + 28,
            LogLevel.Error,
            "Failed to process request from {RemoteEndPoint}",
            EventName = EventIdPrefix + "." + nameof(FailedToProcessRequest)
        )]
        public static partial void FailedToProcessRequest(this ILogger logger, EndPoint? remoteEndPoint, Exception e);

        [LoggerMessage(
            EventIdOffset + 29,
            LogLevel.Information,
            "Connection was reset by {RemoteEndPoint}",
            EventName = EventIdPrefix + "." + nameof(ConnectionWasResetByClient)
        )]
        public static partial void ConnectionWasResetByClient(this ILogger logger, EndPoint? remoteEndPoint);

        [LoggerMessage(
            EventIdOffset + 30,
            LogLevel.Critical,
            "Transition to follower state has failed",
            EventName = EventIdPrefix + "." + nameof(TransitionToFollowerStateFailed)
        )]
        public static partial void TransitionToFollowerStateFailed(this ILogger logger, Exception e);

        [LoggerMessage(
            EventIdOffset + 31,
            LogLevel.Critical,
            "Transition to candidate state has failed",
            EventName = EventIdPrefix + "." + nameof(TransitionToCandidateStateFailed)
        )]
        public static partial void TransitionToCandidateStateFailed(this ILogger logger, Exception e);

        [LoggerMessage(
            EventIdOffset + 32,
            LogLevel.Critical,
            "Transition to leader state has failed",
            EventName = EventIdPrefix + "." + nameof(TransitionToLeaderStateFailed)
        )]
        public static partial void TransitionToLeaderStateFailed(this ILogger logger, Exception e);

        [LoggerMessage(
            EventIdOffset + 33,
            LogLevel.Debug,
            "Follower loop stopped with error",
            EventName = EventIdPrefix + "." + nameof(FollowerStateExitedFailed)
        )]
        public static partial void FollowerStateExitedFailed(this ILogger logger, Exception e);

        [LoggerMessage(
            EventIdOffset + 34,
            LogLevel.Debug,
            "Candidate state reverted with error",
            EventName = EventIdPrefix + "." + nameof(CandidateStateExitedWithError)
        )]
        public static partial void CandidateStateExitedWithError(this ILogger logger, Exception e);

        [LoggerMessage(
            EventIdOffset + 35,
            LogLevel.Debug,
            "Leader state reverted with error",
            EventName = EventIdPrefix + "." + nameof(LeaderStateExitedWithError)
        )]
        public static partial void LeaderStateExitedWithError(this ILogger logger, Exception e);
    }
}