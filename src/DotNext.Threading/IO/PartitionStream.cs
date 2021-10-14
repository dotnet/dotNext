using static System.Globalization.CultureInfo;

namespace DotNext.IO;

internal sealed class PartitionStream : FileStream
{
    internal readonly long PartitionNumber;

    internal PartitionStream(DirectoryInfo location, long partitionNumber, in FileCreationOptions options, int bufferSize)
        : base(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), options.ToFileStreamOptions(bufferSize))
    {
        if (Length == 0L && options.InitialSize > 0L)
            SetLength(options.InitialSize);

        PartitionNumber = partitionNumber;
    }
}