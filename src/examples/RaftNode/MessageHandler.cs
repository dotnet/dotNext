using System;
using System.Threading.Tasks;
using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace RaftNode
{
    internal sealed class MessageHandler : IMessageHandler
    {
        private readonly IServiceProvider services;

        public MessageHandler(IServiceProvider provider) => services = provider;

        public Task<IMessage> ReceiveMessage(IAddressee sender, IMessage message) => Task.FromResult(message);

        public async Task ReceiveSignal(IAddressee sender, IMessage signal)
        {
            var log = services.GetRequiredService<IRaftCluster>().AuditTrail;
            //commit to log entry
            var content = await signal.ReadAsTextAsync().ConfigureAwait(false);
            Console.WriteLine($"Message {content} is received from {sender.Endpoint} and saved into local log");
            ILogEntry[] entries = {new TextMessageFromFile(content) {Term = log.Term}};
            await log.AppendAsync(entries).ConfigureAwait(false);
        }
    }
}
