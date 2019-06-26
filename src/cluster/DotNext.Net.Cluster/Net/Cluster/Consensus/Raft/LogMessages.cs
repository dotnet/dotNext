using System;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Resources;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    [SuppressMessage("Globalization", "CA1304", Justification = "This is culture-specific resource strings")]
    [SuppressMessage("Globalization", "CA1305", Justification = "This is culture-specific resource strings")]
    internal static class LogMessages
    {
        private static readonly ResourceManager Resources = new ResourceManager("DotNext.Net.Cluster.Consensus.Raft.LogMessages", Assembly.GetExecutingAssembly());

        internal static void DowngradingToFollowerState(this ILogger logger)
            => logger.LogInformation(Resources.GetString("DowngradingToFollowerState"));

        internal static void DowngradedToFollowerState(this ILogger logger)
            => logger.LogInformation(Resources.GetString("DowngradedToFollowerState"));

        internal static void TransitionToFollowerStateStarted(this ILogger logger)
            => logger.LogInformation(Resources.GetString("TransitionToFollowerStateStarted"));

        internal static void TransitionToFollowerStateCompleted(this ILogger logger)
            => logger.LogInformation(Resources.GetString("TransitionToFollowerStateCompleted"));

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

        internal static void TimeoutReset(this ILogger logger)
            => logger.LogDebug(Resources.GetString("TimeoutReset"));

        internal static void SendingHearbeat(this ILogger logger, IPEndPoint member)
            => logger.LogDebug(Resources.GetString("SendingHearbeat"), member);

        internal static void ReplicationStarted(this ILogger logger, IPEndPoint member, in LogEntryId record)
            => logger.LogInformation(Resources.GetString("ReplicationStarted"), member, record.Term, record.Index);

        internal static void ReplicationCompleted(this ILogger logger, IPEndPoint member, in LogEntryId record)
            => logger.LogInformation(Resources.GetString("ReplicationCompleted"), member, record.Term, record.Index);
    }
}
