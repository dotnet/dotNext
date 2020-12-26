using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static Xunit.Assert;

namespace DotNext.Net.Cluster.Messaging
{
    [ExcludeFromCodeCoverage]
    internal sealed class Mailbox : ConcurrentQueue<StreamMessage>, IInputChannel
    {
        async Task<IMessage> IInputChannel.ReceiveMessage(ISubscriber sender, IMessage message, object context, CancellationToken token)
        {
            Equal("Request", message.Name);
            Equal("text/plain", message.Type.MediaType);
            var text = await message.ReadAsTextAsync(token);
            Equal("Ping", text);
            return new TextMessage("Pong", "Reply");
        }

        async Task IInputChannel.ReceiveSignal(ISubscriber sender, IMessage signal, object context, CancellationToken token)
        {
            var buffered = new StreamMessage(new MemoryStream(), false, signal.Name, signal.Type);
            await buffered.LoadFromAsync(signal, token).ConfigureAwait(false);
            Enqueue(buffered);
        }
    }
}