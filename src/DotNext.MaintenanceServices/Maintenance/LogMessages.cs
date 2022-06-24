using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace DotNext.Maintenance
{
    [ExcludeFromCodeCoverage]
    internal static partial class LogMessages
    {
        private const string EventIdPrefix = "DotNext.Maintenance";
        private const int EventIdOffset = 80000;

        [LoggerMessage(
            EventIdOffset,
            LogLevel.Debug,
            "Failed to process command received through {EndPoint} Unix Domain Socket",
            EventName = EventIdPrefix + "." + nameof(FailedToProcessCommand)
        )]
        internal static partial void FailedToProcessCommand(this ILogger logger, UnixDomainSocketEndPoint endPoint, Exception e);
    }
}