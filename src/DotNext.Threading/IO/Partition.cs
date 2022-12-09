using static System.Globalization.CultureInfo;

namespace DotNext.IO;

internal sealed class Partition : Disposable
{
    internal readonly long PartitionNumber;
    private readonly FileStream fs;

    internal Partition(DirectoryInfo location, long partitionNumber, in FileStreamFactory factory, int bufferSize, out bool created)
    {
        fs = factory.CreateStream(Path.Combine(location.FullName, partitionNumber.ToString(InvariantCulture)), bufferSize);
        if ((created = fs is { Length: 0L }) && factory.InitialSize > 0L)
            fs.SetLength(factory.InitialSize);

        PartitionNumber = partitionNumber;
    }

    internal Stream Stream => fs;

    internal string FileName => fs.Name;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            fs.Dispose();
        }

        base.Dispose(disposing);
    }
}