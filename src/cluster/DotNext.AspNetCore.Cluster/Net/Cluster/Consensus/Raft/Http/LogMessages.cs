using System;
using System.Net;
using System.Reflection;
using System.Resources;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Resources;

    internal static class LogMessages
    {
        private static readonly ResourceManager Resources = new ResourceManager("DotNext.Net.Cluster.Consensus.Raft.Http.LogMessages", Assembly.GetExecutingAssembly());

        internal static void SendingRequestToMember(this ILogger logger, EndPoint member, string messageType)
            => logger.LogDebug((string)Resources.Get(), messageType, member);

        internal static void MemberUnavailable(this ILogger logger, EndPoint member, Exception e)
            => logger.LogWarning(e, (string)Resources.Get(), member);

        internal static void UnhandledException(this ILogger logger, Exception e)
            => logger.LogError(e, (string)Resources.Get());

        internal static void FailedToRouteMessage(this ILogger logger, string messageName, Exception e)
            => logger.LogInformation(e, (string)Resources.Get(), messageName);
    }
}
