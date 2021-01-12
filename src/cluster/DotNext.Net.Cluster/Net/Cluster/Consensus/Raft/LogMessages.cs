using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Resources;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Resources;

    internal static class LogMessages
    {
        private static readonly ResourceManager Resources = new ResourceManager("DotNext.Net.Cluster.Consensus.Raft.LogMessages", Assembly.GetExecutingAssembly());

        internal static void DowngradingToFollowerState(this ILogger logger)
            => logger.LogDebug((string)Resources.Get());

        internal static void DowngradedToFollowerState(this ILogger logger)
            => logger.LogDebug((string)Resources.Get());

        internal static void TransitionToCandidateStateStarted(this ILogger logger)
            => logger.LogInformation((string)Resources.Get());

        internal static void TransitionToCandidateStateCompleted(this ILogger logger)
            => logger.LogInformation((string)Resources.Get());

        internal static void TransitionToLeaderStateStarted(this ILogger logger)
            => logger.LogInformation((string)Resources.Get());

        internal static void TransitionToLeaderStateCompleted(this ILogger logger)
            => logger.LogInformation((string)Resources.Get());

        internal static void VotingStarted(this ILogger logger, int timeout)
            => logger.LogDebug((string)Resources.Get(), timeout);

        internal static void VotingCompleted(this ILogger logger, int votes)
            => logger.LogDebug((string)Resources.Get(), votes);

        internal static void VoteGranted(this ILogger logger, EndPoint member)
            => logger.LogDebug((string)Resources.Get(), member);

        internal static void VoteRejected(this ILogger logger, EndPoint member)
            => logger.LogDebug((string)Resources.Get(), member);

        internal static void MemberUnavailable(this ILogger logger, EndPoint member)
            => logger.LogDebug((string)Resources.Get(), member);

        internal static void MemberUnavailable(this ILogger logger, EndPoint member, Exception e)
            => logger.LogWarning((string)Resources.Get(), member, e);

        internal static void TimeoutReset(this ILogger logger)
            => logger.LogDebug((string)Resources.Get());

        internal static void ReplicationStarted(this ILogger logger, EndPoint member, long index)
            => logger.LogDebug((string)Resources.Get(), member, index);

        internal static void ReplicaSize(this ILogger logger, EndPoint? member, int count, long index, long term)
            => logger.LogDebug((string)Resources.Get(), member, count, index, term);

        internal static void ReplicationSuccessful(this ILogger logger, EndPoint member, long index)
            => logger.LogDebug((string)Resources.Get(), member, index);

        internal static void ReplicationFailed(this ILogger logger, EndPoint member, long index)
            => logger.LogInformation((string)Resources.Get(), member, index);

        internal static void CommitFailed(this ILogger logger, int quorum, long commitIndex)
            => logger.LogDebug((string)Resources.Get(), quorum, commitIndex);

        internal static void CommitSuccessful(this ILogger logger, long index, long count)
            => logger.LogDebug((string)Resources.Get(), index, count);

        internal static void InstallingSnapshot(this ILogger logger, long snapshotIndex)
            => logger.LogDebug((string)Resources.Get(), snapshotIndex);

        internal static string SnapshotInstallationFailed => (string)Resources.Get();

        internal static void PacketDropped<T>(this ILogger logger, T packetId, EndPoint? endPoint)
            where T : struct
            => logger.LogError((string)Resources.Get(), packetId.ToString(), endPoint);

        internal static void NotEnoughRequestHandlers(this ILogger logger)
            => logger.LogError((string)Resources.Get());

        internal static void SockerErrorOccurred(this ILogger logger, SocketError error)
            => logger.LogError((string)Resources.Get(), error);

        internal static void RequestTimedOut(this ILogger logger)
            => logger.LogWarning((string)Resources.Get());

        internal static void SocketAcceptLoopTerminated(this ILogger logger, Exception e)
            => logger.LogDebug(e, (string)Resources.Get());

        internal static void TcpGracefulShutdownFailed(this ILogger logger, int timeout)
            => logger.LogWarning((string)Resources.Get(), timeout);

        internal static void FailedToCancelPendingRequests(this ILogger logger, Exception e)
            => logger.LogWarning(e, (string)Resources.Get());

        internal static void StopAsyncWasNotCalled(this ILogger logger)
            => logger.LogWarning((string)Resources.Get());
    }
}
