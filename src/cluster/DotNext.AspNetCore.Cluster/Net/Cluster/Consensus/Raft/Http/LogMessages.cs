using System.Net;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal static partial class LogMessages
    {
        private const string EventIdPrefix = "DotNext.AspNetCore.Cluster";
        private const int EventIdOffset = 75000;

        [LoggerMessage(
            EventIdOffset,
            LogLevel.Debug,
            "Sending request of type {MessageType} to member {Member}",
            EventName = EventIdPrefix + "." + nameof(SendingRequestToMember)
        )]
        public static partial void SendingRequestToMember(this ILogger logger, EndPoint member, string messageType);

        [LoggerMessage(
            EventIdOffset + 1,
            LogLevel.Warning,
            "Cluster member {Member} is unavailable",
            EventName = EventIdPrefix + "." + nameof(MemberUnavailable)
        )]
        public static partial void MemberUnavailable(this ILogger logger, EndPoint member, Exception e);

        [LoggerMessage(
            EventIdOffset + 2,
            LogLevel.Error,
            "An unhandled exception has occurred while executing the request",
            EventName = EventIdPrefix + "." + nameof(UnhandledException)
        )]
        public static partial void UnhandledException(this ILogger logger, Exception e);

        [LoggerMessage(
            EventIdOffset + 3,
            LogLevel.Information,
            "Failed to route message {MessageName} to leader node",
            EventName = EventIdPrefix + "." + nameof(FailedToRouteMessage)
        )]
        public static partial void FailedToRouteMessage(this ILogger logger, string messageName, Exception e);
    }
}