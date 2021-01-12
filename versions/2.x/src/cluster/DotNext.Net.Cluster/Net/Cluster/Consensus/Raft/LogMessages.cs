using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Resources;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    [SuppressMessage("Globalization", "CA1304", Justification = "This is culture-specific resource strings")]
    [SuppressMessage("Globalization", "CA1305", Justification = "This is culture-specific resource strings")]
    internal static class LogMessages
    {
        private static readonly ResourceManager Resources = new ResourceManager("DotNext.Net.Cluster.Consensus.Raft.LogMessages", Assembly.GetExecutingAssembly());

        internal static void DowngradingToFollowerState(this ILogger logger)
            => logger.LogDebug(Resources.GetString("DowngradingToFollowerState"));

        internal static void DowngradedToFollowerState(this ILogger logger)
            => logger.LogDebug(Resources.GetString("DowngradedToFollowerState"));

        internal static void TransitionToCandidateStateStarted(this ILogger logger)
            => logger.LogInformation(Resources.GetString("TransitionToCandidateStateStarted"));

        internal static void TransitionToCandidateStateCompleted(this ILogger logger)
            => logger.LogInformation(Resources.GetString("TransitionToCandidateStateCompleted"));

        internal static void TransitionToLeaderStateStarted(this ILogger logger)
            => logger.LogInformation(Resources.GetString("TransitionToLeaderStateStarted"));

        internal static void TransitionToLeaderStateCompleted(this ILogger logger)
            => logger.LogInformation(Resources.GetString("TransitionToLeaderStateCompleted"));

        internal static void VotingStarted(this ILogger logger, int timeout)
            => logger.LogDebug(Resources.GetString("VotingStarted"), timeout);

        internal static void VotingCompleted(this ILogger logger, int votes)
            => logger.LogDebug(Resources.GetString("VotingCompleted"), votes);

        internal static void VoteGranted(this ILogger logger, IPEndPoint member)
            => logger.LogDebug(Resources.GetString("VoteGranted"), member);

        internal static void VoteRejected(this ILogger logger, IPEndPoint member)
            => logger.LogDebug(Resources.GetString("VoteRejected"), member);

        internal static void MemberUnavailable(this ILogger logger, IPEndPoint member)
            => logger.LogDebug(Resources.GetString("MemberUnavailable"), member);

        internal static void MemberUnavailable(this ILogger logger, IPEndPoint member, Exception e)
            => logger.LogWarning(Resources.GetString("MemberUnavailable"), member, e);

        internal static void TimeoutReset(this ILogger logger)
            => logger.LogDebug(Resources.GetString("TimeoutReset"));

        internal static void ReplicationStarted(this ILogger logger, IPEndPoint member, long index)
            => logger.LogDebug(Resources.GetString("ReplicationStarted"), member, index);

        internal static void ReplicaSize(this ILogger logger, IPEndPoint member, int count, long index, long term)
            => logger.LogDebug(Resources.GetString("ReplicaSize"), member, count, index, term);

        internal static void ReplicationSuccessful(this ILogger logger, IPEndPoint member, long index)
            => logger.LogDebug(Resources.GetString("ReplicationSuccessful"), member, index);

        internal static void ReplicationFailed(this ILogger logger, IPEndPoint member, long index)
            => logger.LogInformation(Resources.GetString("ReplicationFailed"), member, index);

        internal static void CommitFailed(this ILogger logger, int quorum, long commitIndex)
            => logger.LogDebug(Resources.GetString("CommitFailed"), quorum, commitIndex);

        internal static void CommitSuccessful(this ILogger logger, long index, long count)
            => logger.LogDebug(Resources.GetString("CommitSuccessful"), index, count);

        internal static void InstallingSnapshot(this ILogger logger, long snapshotIndex)
            => logger.LogDebug(Resources.GetString("InstallingSnapshot"), snapshotIndex);

        internal static string SnapshotInstallationFailed => Resources.GetString("SnapshotInstallationFailed");

        internal static Task PrintLockInfoTitle(this TextWriter output) => output.WriteLineAsync(Resources.GetString("LockInfoTitle"));

        internal static Task PrintLockInfo(this TextWriter output, string lockName, string owner, in Guid version, DateTimeOffset creationTime, TimeSpan leaseTime)
            => output.WriteLineAsync(string.Format(Resources.GetString("LockInfo"), lockName, owner, version.ToString(), creationTime.ToString(InvariantCulture), leaseTime.ToString()));

        internal static void PacketDropped<T>(this ILogger logger, T packetId, EndPoint endPoint)
            where T : struct
            => logger.LogError(Resources.GetString("PacketDropped"), packetId.ToString(), endPoint);

        internal static void NotEnoughRequestHandlers(this ILogger logger)
            => logger.LogError(Resources.GetString("NotEnoughRequestHandlers"));

        internal static void SockerErrorOccurred(this ILogger logger, SocketError error)
            => logger.LogError(Resources.GetString("SockerErrorOccurred"), error);

        internal static void RequestTimedOut(this ILogger logger)
            => logger.LogWarning(Resources.GetString("RequestTimedOut"));

        internal static void SocketAcceptLoopTerminated(this ILogger logger, Exception e)
            => logger.LogDebug(e, Resources.GetString("SocketAcceptLoopTerminated"));

        internal static void TcpGracefulShutdownFailed(this ILogger logger, int timeout)
            => logger.LogWarning(Resources.GetString("TcpGracefulShutdownFailed"), timeout);

        internal static void FailedToCancelPendingRequests(this ILogger logger, Exception e)
            => logger.LogWarning(e, Resources.GetString("FailedToCancelPendingRequests"));
    }
}
