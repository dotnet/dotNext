using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading.Channels;

using IO;

internal interface IChannel
{
    DirectoryInfo Location { get; }

    void GetOrCreatePartition(ref ChannelCursor cursor, [NotNull] ref Partition? partition, in FileStreamFactory factory, bool deleteOnDispose);

    Task Completion { get; }
}