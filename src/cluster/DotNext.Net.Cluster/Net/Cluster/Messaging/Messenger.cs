using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    public static class Messenger
    {
        public static Task SendBroadcastSignalAsync(this ICluster cluster, IMessage message, bool requiresConfirmation = true)
        {
            ICollection<Task> tasks = new LinkedList<Task>();
            foreach(var member in cluster)
                if(member.IsRemote && member is IMessenger messenger)
                    tasks.Add(messenger.SendSignalAsync(message, requiresConfirmation));
            return Task.WhenAll(tasks);
        }

        public static Task<IMessage> SendTextMessageAsync(this IMessenger messenger, string messageName, string text, string mediaType = null, CancellationToken token = default)
            => messenger.SendMessageAsync(new TextMessage(messageName, text, mediaType), token);

        public static Task SendTextSignalAsync(this IMessenger messenger, string messageName, string text, string mediaType = null, bool requiresConfirmation = true)
            => messenger.SendSignalAsync(new TextMessage(messageName, text, mediaType), requiresConfirmation);
    }
}