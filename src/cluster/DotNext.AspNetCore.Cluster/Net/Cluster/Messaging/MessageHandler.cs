using static System.Linq.Enumerable;

namespace DotNext.Net.Cluster.Messaging;

internal static class MessageHandler
{
    internal static bool IsSignalSupported(this IMessage signal, IInputChannel handler)
        => handler.IsSupported(signal.Name, true);

    private static bool IsMessageSupported(this IMessage request, IInputChannel handler)
        => handler.IsSupported(request.Name, false);

    internal static Task<IMessage>? TryReceiveMessage(this IEnumerable<IInputChannel> chain, ISubscriber sender, IMessage message, object? context, CancellationToken token)
        => chain.FirstOrDefault(message.IsMessageSupported)?.ReceiveMessage(sender, message, context, token);

    internal static Task? TryReceiveSignal(this IEnumerable<IInputChannel> chain, ISubscriber sender, IMessage signal, object? context, CancellationToken token)
        => chain.FirstOrDefault(signal.IsSignalSupported)?.ReceiveSignal(sender, signal, context, token);
}