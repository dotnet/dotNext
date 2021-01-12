using System.IO;
using static System.Globalization.CultureInfo;

namespace DotNext.IO
{
    internal sealed class PartitionStream : FileStream
    {
        internal PartitionStream(DirectoryInfo location, long partitionNumber, in FileCreationOptions options, int bufferSize)
            : base(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), options.Mode, options.Access, options.Share, bufferSize, options.Optimization)
        {
            PartitionNumber = partitionNumber;
        }

        internal long PartitionNumber { get; }
    }
}
