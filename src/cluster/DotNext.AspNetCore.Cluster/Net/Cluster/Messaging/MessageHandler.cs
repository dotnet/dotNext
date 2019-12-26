using System.Collections.Generic;
using System.Threading.Tasks;
using static System.Linq.Enumerable;

namespace DotNext.Net.Cluster.Messaging
{
    internal static class MessageHandler
    {
        internal static bool IsSignalSupported(this IMessage signal, IMessageHandler handler)
            => handler.IsSupported(signal.Name, true);
        
        private static bool IsMessageSupported(this IMessage request, IMessageHandler handler)
            => handler.IsSupported(request.Name, false);

        internal static Task<IMessage>? TryReceiveMessage(this IEnumerable<IMessageHandler> chain, ISubscriber sender, IMessage message, object? context)
            => chain.FirstOrDefault(message.IsMessageSupported)?.ReceiveMessage(sender, message, context);

        internal static Task? TryReceiveSignal(this IEnumerable<IMessageHandler> chain, ISubscriber sender, IMessage signal, object? context)
            => chain.FirstOrDefault(signal.IsSignalSupported)?.ReceiveSignal(sender, signal, context);
    }
}