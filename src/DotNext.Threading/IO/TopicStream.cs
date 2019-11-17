using System.IO;
using static System.Globalization.CultureInfo;

namespace DotNext.IO
{
    internal sealed class TopicStream : FileStream
    {
        internal TopicStream(DirectoryInfo location, long partitionNumber, FileMode mode, FileAccess access, FileShare share, int bufferSize)
            : base(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), mode, access, share, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough)
        {
            PartitionNumber = partitionNumber;
        }

        internal long PartitionNumber { get; }
    }
}
