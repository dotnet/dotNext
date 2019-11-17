using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading.Channels
{
    using IO;

    internal interface IChannelWriter<T> : IChannel
    {
        bool TryComplete(Exception e = null);

        void MessageReady();

        ValueTask SerializeAsync(T input, TopicStream output, CancellationToken token);
    }
}
