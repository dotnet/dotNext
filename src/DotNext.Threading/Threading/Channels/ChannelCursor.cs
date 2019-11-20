using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Channels
{
    /*
     * State file format:
     * [8 bytes] = message number
     * [8 bytes] = offset in stream
     */
    [StructLayout(LayoutKind.Auto)]
    internal struct ChannelCursor
    {
        private const long StateFileSize = sizeof(long) + sizeof(long);
        private const long PositionOffset = 0L;
        private const long OffsetOffset = PositionOffset + sizeof(long);
        private readonly MemoryMappedFile stateFile;
        private readonly MemoryMappedViewAccessor stateView;
        private long position;
        private long offset;

        internal ChannelCursor(DirectoryInfo location, string stateFileName)
        {
            stateFileName = Path.Combine(location.FullName, stateFileName);
            stateFile = MemoryMappedFile.CreateFromFile(stateFileName, FileMode.OpenOrCreate, null, StateFileSize, MemoryMappedFileAccess.ReadWrite);
            stateView = stateFile.CreateViewAccessor();
            position = stateView.ReadInt64(PositionOffset);
            offset = stateView.ReadInt64(OffsetOffset);
        }

        internal long Position => position;

        internal void Adjust(Stream stream) => stream.Position = offset;

        internal void Reset()
            => stateView.Write(OffsetOffset, offset = 0L);

        internal void Advance(long offset)
        {
            stateView.Write(PositionOffset, position += 1L);
            stateView.Write(OffsetOffset, this.offset = offset);
            stateView.Flush();
        }

        public void Dispose()
        {
            stateView?.Dispose();
            stateFile?.Dispose();
            this = default;
        }
    }
}
