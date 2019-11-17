using System.IO;

namespace DotNext.Threading.Channels
{
    using IO;

    internal interface IChannel
    {
        DirectoryInfo Location { get; }

        PartitionStream GetOrCreatePartition(ref ChannelCursor cursor, ref PartitionStream partition, in FileCreationOptions options, bool deleteOnDispose);
    }
}
