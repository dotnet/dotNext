using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using static Xunit.Assert;

namespace DotNext.Net.Cluster.Messaging
{
    using static Mime.ContentTypeExtensions;

    internal sealed class Mailbox : ConcurrentQueue<StreamMessage>, IMessageHandler
    {
        internal static async Task<string> ReadAsText(IMessage message)
        {
            using (var ms = new MemoryStream(1024))
            {
                await message.CopyToAsync(ms);
                ms.Seek(0, SeekOrigin.Begin);
                using (var reader = new StreamReader(ms, message.Type.GetEncoding(), false, 1024, true))
                    return reader.ReadToEnd();
            }
        }

        async Task<IMessage> IMessageHandler.ReceiveMessage(IAddressee sender, IMessage message)
        {
            Equal("Request", message.Name);
            Equal("text/plain", message.Type.MediaType);
            var text = await ReadAsText(message);
            Equal("Ping", text);
            return new TextMessage("Pong", "Reply");
        }

        async Task IMessageHandler.ReceiveSignal(IAddressee sender, IMessage signal)
           => Enqueue(await StreamMessage.CreateBufferedMessageAsync(signal).ConfigureAwait(false));
    }
}