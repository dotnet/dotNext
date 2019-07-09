using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace RaftNode
{
    internal sealed class MessageHandler : IMessageHandler
    {
        private readonly IServiceProvider services;

        public MessageHandler(IServiceProvider provider) => services = provider;

        public Task<IMessage> ReceiveMessage(IAddressee sender, IMessage message, object context) => Task.FromResult(message);

        public async Task ReceiveSignal(IAddressee sender, IMessage signal, object context)
        {
            var log = services.GetRequiredService<IRaftCluster>().AuditTrail;
            //commit to log entry
            var content = await signal.ReadAsTextAsync().ConfigureAwait(false);
            var entry = new TextMessageFromFile(content) { Term = log.Term };
            Console.WriteLine(
                $"Message {content} is received from {sender.Endpoint} and saved into local log, current term is {entry.Term}");
            await log.AppendAsync(new[] { entry }).ConfigureAwait(false);
        }
    }
}
