using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    [ExcludeFromCodeCoverage]
    public class TestMessageHandler : MessageHandler
    {
        internal int Result;

        public Task<ResultMessage> AddAsync(AddMessage message, CancellationToken token)
        {
            return Task.FromResult<ResultMessage>(new() { Result = message.Execute() });
        }

        public Task<ResultMessage> SubtractAsync(ISubscriber sender, SubtractMessage message, CancellationToken token)
        {
            return Task.FromResult<ResultMessage>(new() { Result = message.Execute() });
        }

        public Task ReceiveAsync(ResultMessage signal, object context, CancellationToken token)
        {
            Result = signal.Result;
            return Task.CompletedTask;
        }
    }
}