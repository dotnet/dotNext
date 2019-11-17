using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading.Channels
{
    using IO;

    internal interface IChannelReader<T> : IChannel, IDisposable
    {
        Task CompletionTask { get; }

        Task WaitToReadAsync(CancellationToken token);

        ValueTask<T> DeserializeAsync(TopicStream input, CancellationToken token);
    }
}
