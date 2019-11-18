using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading.Channels
{
    using IO;

    internal interface IChannelWriter<T> : IChannel
    {
        void MessageReady();

        ValueTask SerializeAsync(T input, PartitionStream output, CancellationToken token);
    }
}
