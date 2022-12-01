using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Discovery.HyParView.Http;

internal static partial class LogMessages
{
    private const string EventIdPrefix = "DotNext.Net.HyParView.Http";
    private const int EventIdOffset = 77000;

    [LoggerMessage(
        EventIdOffset,
        LogLevel.Warning,
        "No contact node provider, the current peer remains alone",
        EventName = EventIdPrefix + "." + nameof(NoContactNodeProvider)
    )]
    public static partial void NoContactNodeProvider(this ILogger logger);
}