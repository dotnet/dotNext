using System.Diagnostics;
using System.Runtime.Versioning;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using IO;

partial class SimpleStateMachine
{
    [SupportedOSPlatform("linux")]
    private sealed unsafe class LinuxSnapshotWriter(
        long preallocationSize,
        FileInfo destination,
        delegate*unmanaged<byte*, int, int, int> openFileFunction)
        : SnapshotWriter(preallocationSize, destination)
    {
        public static Func<long, FileInfo, LinuxSnapshotWriter> CreateFactory(delegate*unmanaged<byte*, int, int, int> openFileFunction)
        {
            Debug.Assert(openFileFunction is not null);

            return (size, dest) => new LinuxSnapshotWriter(size, dest, openFileFunction);
        }

        protected override void Commit(string sourceFileName, string destinationFileName)
        {
            base.Commit(sourceFileName, destinationFileName);
            Directory.FlushToDisk(Path.GetDirectoryName(destinationFileName.AsSpan()), openFileFunction);
        }
    }
}