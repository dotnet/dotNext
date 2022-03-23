using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading.Channels;

using IO;

internal interface IChannel
{
    DirectoryInfo Location { get; }

    PartitionStream GetOrCreatePartition(ref ChannelCursor cursor, [NotNull] ref PartitionStream? partition, in FileCreationOptions options, bool deleteOnDispose);

    Task Completion { get; }
}