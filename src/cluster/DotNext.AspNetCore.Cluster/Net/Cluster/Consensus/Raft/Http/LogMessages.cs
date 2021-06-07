using System;
using System.Net;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal static class LogMessages
    {
        private const string EventIdPrefix = "DotNext.AspNetCore.Cluster";
        private const int EventIdOffset = 75000;
        private static readonly ResourceManager Resources = new("DotNext.Net.Cluster.Consensus.Raft.Http.LogMessages", Assembly.GetExecutingAssembly());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Log<T>(this ILogger logger, LogLevel level, int eventId, T args, Exception? e = null, [CallerMemberName] string resourceName = "")
            where T : struct, ITuple
        {
            if (logger.IsEnabled(level))
                logger.Log(level, new(EventIdOffset + eventId, string.Concat(EventIdPrefix, ".", resourceName)), e, Resources.GetString(resourceName)!, args.ToArray());
        }

        internal static void SendingRequestToMember(this ILogger logger, EndPoint member, string messageType)
            => logger.Log(LogLevel.Debug, 0, (messageType, member));

        internal static void MemberUnavailable(this ILogger logger, EndPoint member, Exception e)
            => logger.Log(LogLevel.Warning, 1, ValueTuple.Create(member), e);

        internal static void UnhandledException(this ILogger logger, Exception e)
            => logger.Log(LogLevel.Error, 2, new ValueTuple(), e);

        internal static void FailedToRouteMessage(this ILogger logger, string messageName, Exception e)
            => logger.Log(LogLevel.Information, 3, ValueTuple.Create(messageName), e);
    }
}
