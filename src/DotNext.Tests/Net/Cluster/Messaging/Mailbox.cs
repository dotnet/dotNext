using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static Xunit.Assert;

namespace DotNext.Net.Cluster.Messaging
{
    internal sealed class Mailbox : ConcurrentQueue<IMessage>, IMessageHandler
    {
        internal static async Task<string> ReadAsText(IMessage message)
        {
            using(var ms = new MemoryStream(1024))
            using(var reader = new StreamReader(ms, Encoding.UTF8))
            {
                await message.CopyToAsync(ms);
                ms.Position = 0;
                return reader.ReadToEnd();
            }
        }

        async ValueTask<IMessage> IMessageHandler.ReceiveMessage(IAddressee sender, IMessage message)
        {
            Equal("Request", message.Name);
            Equal("text/plain", message.Type.MediaType);
            Equal("Ping", await ReadAsText(message));
            return new TextMessage("Reply", "Pong");
        }

         ValueTask IMessageHandler.ReceiveSignal(IAddressee sender, IMessage signal)
         {
            Enqueue(signal);
            return new ValueTask();
         }
    }
}