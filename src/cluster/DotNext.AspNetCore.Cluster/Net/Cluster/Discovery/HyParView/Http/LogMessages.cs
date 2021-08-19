using System;
using System.Net;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Discovery.HyParView.Http
{
    internal static class LogMessages
    {
        private const string EventIdPrefix = "DotNext.Net.HyParView.Http";
        private const int EventIdOffset = 77000;
        private static readonly ResourceManager Resources = new("DotNext.Net.Cluster.Discovery.HyParView.Http.LogMessages", Assembly.GetExecutingAssembly());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Log<T>(this ILogger logger, LogLevel level, int eventId, T args, Exception? e = null, [CallerMemberName] string resourceName = "")
            where T : struct, ITuple
        {
            if (logger.IsEnabled(level))
                logger.Log(level, new(EventIdOffset + eventId, string.Concat(EventIdPrefix, ".", resourceName)), e, Resources.GetString(resourceName)!, args.ToArray());
        }

        internal static void NoContactNodeProvider(this ILogger logger)
            => logger.Log(LogLevel.Warning, 0, ValueTuple.Create());
    }
}
