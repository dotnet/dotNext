using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace DotNext.Threading.Channels;

using IO;

internal interface IChannel
{
    private const string LocationMeterAttribute = "dotnext.threading.channels.persistentchannel.path";

    internal static readonly Counter<int> ReadRateMeter, WriteRateMeter;

    static IChannel()
    {
        var meter = new Meter("DotNext.Threading.Channels.PersistentChannel");
        ReadRateMeter = meter.CreateCounter<int>("ReadRate");
        WriteRateMeter = meter.CreateCounter<int>("WriteRate");
    }

    private protected static void SetTags(ref TagList tags, string path)
        => tags.Add(LocationMeterAttribute, path);

    DirectoryInfo Location { get; }

    void GetOrCreatePartition(ref ChannelCursor cursor, [NotNull] ref Partition? partition, in FileStreamFactory factory, bool deleteOnDispose);

    Task Completion { get; }

    ref readonly TagList MeasurementTags { get; }
}