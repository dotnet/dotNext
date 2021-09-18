using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Discovery.HyParView
{
    [ExcludeFromCodeCoverage]
    internal static partial class LogMessages
    {
        private const string EventIdPrefix = "DotNext.Net.Cluster.HyParView";
        private const int EventIdOffset = 76000;

        [LoggerMessage(
            EventIdOffset,
            LogLevel.Warning,
            "Command {CommandType} processing failed",
            EventName = EventIdPrefix + "." + nameof(FailedToProcessCommand)
        )]
        public static partial void FailedToProcessCommand(this ILogger logger, int commandType, Exception e);

        [LoggerMessage(
            EventIdOffset + 1,
            LogLevel.Warning,
            "Communication with peer {Peer} has failed",
            EventName = EventIdPrefix + "." + nameof(PeerCommunicationFailed)
        )]
        public static partial void PeerCommunicationFailed(this ILogger logger, EndPoint peer, Exception e);
    }
}