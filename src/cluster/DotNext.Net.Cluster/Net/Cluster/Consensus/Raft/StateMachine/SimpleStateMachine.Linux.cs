using System.Runtime.Versioning;
using System.Text;
using DotNext.Buffers;
using Microsoft.Win32.SafeHandles;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

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
            => (size, dest) => new LinuxSnapshotWriter(size, dest, openFileFunction);
        
        protected override void Commit(string sourceFileName, string destinationFileName)
        {
            base.Commit(sourceFileName, destinationFileName);
            FlushDirectory(Path.GetDirectoryName(destinationFileName), openFileFunction);
        }

        private static void FlushDirectory(ReadOnlySpan<char> path, delegate*unmanaged<byte*, int, int, int> openFileFunction)
        {
            const int O_RDONLY = 0x0000;

            var byteCount = Encoding.UTF8.GetByteCount(path) + 1;
            int fd;
            using (var pathBuffer = (uint)byteCount <= (uint)SpanOwner<byte>.StackallocThreshold
                       ? stackalloc byte[byteCount]
                       : new SpanOwner<byte>(byteCount))
            {
                Encoding.UTF8.GetBytes(path, pathBuffer.Span);
                pathBuffer[^1] = 0;

                fixed (byte* pathPtr = pathBuffer)
                {
                    fd = openFileFunction(pathPtr, O_RDONLY, 0);
                }
            }

            if (fd >= 0)
            {
                using var handle = new SafeFileHandle(fd, ownsHandle: true);
                RandomAccess.FlushToDisk(handle);
            }
        }
    }
}