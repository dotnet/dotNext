using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Resources;

    // TODO: Migrate to logger codegen in .NET 6
    internal static class LogMessages
    {
        private const string EventIdPrefix = "DotNext.Net.Cluster";
        private const int EventIdOffset = 74000;
        private static readonly ResourceManager Resources = new("DotNext.Net.Cluster.Consensus.Raft.LogMessages", Assembly.GetExecutingAssembly());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Log<T>(this ILogger logger, LogLevel level, int eventId, T args, Exception? e = null, [CallerMemberName] string resourceName = "")
            where T : struct, ITuple
        {
            if (logger.IsEnabled(level))
                logger.Log(level, new(EventIdOffset + eventId, string.Concat(EventIdPrefix, ".", resourceName)), e, Resources.GetString(resourceName)!, args.ToArray());
        }

        internal static void DowngradingToFollowerState(this ILogger logger)
            => logger.Log(LogLevel.Debug, 0, new ValueTuple());

        internal static void DowngradedToFollowerState(this ILogger logger)
            => logger.Log(LogLevel.Debug, 1, new ValueTuple());

        internal static void TransitionToCandidateStateStarted(this ILogger logger)
            => logger.Log(LogLevel.Information, 2, new ValueTuple());

        internal static void TransitionToCandidateStateCompleted(this ILogger logger)
            => logger.Log(LogLevel.Information, 3, new ValueTuple());

        internal static void TransitionToLeaderStateStarted(this ILogger logger)
            => logger.Log(LogLevel.Information, 4, new ValueTuple());

        internal static void TransitionToLeaderStateCompleted(this ILogger logger)
            => logger.Log(LogLevel.Information, 5, new ValueTuple());

        internal static void VotingStarted(this ILogger logger, int timeout)
            => logger.Log(LogLevel.Debug, 6, ValueTuple.Create(timeout));

        internal static void VotingCompleted(this ILogger logger, int votes)
            => logger.Log(LogLevel.Debug, 7, ValueTuple.Create(votes));

        internal static void VoteGranted(this ILogger logger, EndPoint member)
            => logger.Log(LogLevel.Debug, 8, ValueTuple.Create(member));

        internal static void VoteRejected(this ILogger logger, EndPoint member)
            => logger.Log(LogLevel.Debug, 9, ValueTuple.Create(member));

        internal static void MemberUnavailable(this ILogger logger, EndPoint member)
            => logger.Log(LogLevel.Debug, 10, ValueTuple.Create(member));

        internal static void MemberUnavailable(this ILogger logger, EndPoint member, Exception e)
            => logger.Log(LogLevel.Warning, 11, ValueTuple.Create(member), e);

        internal static void TimeoutReset(this ILogger logger)
            => logger.Log(LogLevel.Debug, 12, new ValueTuple());

        internal static void ReplicationStarted(this ILogger logger, EndPoint member, long index)
            => logger.Log(LogLevel.Debug, 13, (member, index));

        internal static void ReplicaSize(this ILogger logger, EndPoint? member, int count, long index, long term)
            => logger.Log(LogLevel.Debug, 14, (member, count, index, term));

        internal static void ReplicationSuccessful(this ILogger logger, EndPoint member, long index)
            => logger.Log(LogLevel.Debug, 15, (member, index));

        internal static void ReplicationFailed(this ILogger logger, EndPoint member, long index)
            => logger.Log(LogLevel.Information, 16, (member, index));

        internal static void CommitFailed(this ILogger logger, int quorum, long commitIndex)
            => logger.Log(LogLevel.Debug, 17, (quorum, commitIndex));

        internal static void CommitSuccessful(this ILogger logger, long index, long count)
            => logger.Log(LogLevel.Debug, 18, (index, count));

        internal static void InstallingSnapshot(this ILogger logger, long snapshotIndex)
            => logger.Log(LogLevel.Debug, 19, ValueTuple.Create(snapshotIndex));

        internal static string SnapshotInstallationFailed => (string)Resources.Get();

        internal static void PacketDropped<T>(this ILogger logger, T packetId, EndPoint? endPoint)
            where T : struct
            => logger.Log(LogLevel.Error, 20, (packetId, endPoint));

        internal static void NotEnoughRequestHandlers(this ILogger logger)
            => logger.Log(LogLevel.Error, 21, new ValueTuple());

        internal static void SockerErrorOccurred(this ILogger logger, SocketError error)
            => logger.Log(LogLevel.Error, 22, ValueTuple.Create(error));

        internal static void RequestTimedOut(this ILogger logger)
            => logger.Log(LogLevel.Warning, 23, new ValueTuple());

        internal static void SocketAcceptLoopTerminated(this ILogger logger, Exception e)
            => logger.Log(LogLevel.Debug, 24, new ValueTuple(), e);

        internal static void TcpGracefulShutdownFailed(this ILogger logger, int timeout)
            => logger.Log(LogLevel.Warning, 25, ValueTuple.Create(timeout));

        internal static void FailedToCancelPendingRequests(this ILogger logger, Exception e)
            => logger.Log(LogLevel.Warning, 26, new ValueTuple(), e);

        internal static void StopAsyncWasNotCalled(this ILogger logger)
            => logger.Log(LogLevel.Warning, 27, new ValueTuple());
    }
}
