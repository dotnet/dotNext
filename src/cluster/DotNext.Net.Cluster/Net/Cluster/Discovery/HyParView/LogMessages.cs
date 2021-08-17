using System;
using System.Net;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Discovery.HyParView
{
    // TODO: Migrate to logger codegen in .NET 6
    internal static class LogMessages
    {
        private const string EventIdPrefix = "DotNext.Net.Cluster.HyParView";
        private const int EventIdOffset = 75000;
        private static readonly ResourceManager Resources = new("DotNext.Net.Cluster.Discovery.HyParView.LogMessages", Assembly.GetExecutingAssembly());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Log<T>(this ILogger logger, LogLevel level, int eventId, T args, Exception? e = null, [CallerMemberName] string resourceName = "")
            where T : struct, ITuple
        {
            if (logger.IsEnabled(level))
                logger.Log(level, new(EventIdOffset + eventId, string.Concat(EventIdPrefix, ".", resourceName)), e, Resources.GetString(resourceName)!, args.ToArray());
        }

        internal static void FailedToProcessCommand(this ILogger logger, int commandType, Exception e)
            => logger.Log(LogLevel.Warning, 0, ValueTuple.Create(commandType), e);

        internal static void PeerCommunicationFailed(this ILogger logger, EndPoint peer, Exception e)
            => logger.Log(LogLevel.Warning, 1, ValueTuple.Create(peer), e);
    }
}
