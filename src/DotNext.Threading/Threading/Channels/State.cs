using System.IO;
using System.IO.MemoryMappedFiles;

namespace DotNext.Threading.Channels
{
    internal struct State
    {
        private const long StateFileSize = sizeof(long);
        private readonly MemoryMappedFile stateFile;
        private readonly MemoryMappedViewAccessor stateView;
        private long position;

        internal State(DirectoryInfo location, string stateFileName)
        {
            stateFileName = Path.Combine(location.FullName, stateFileName);
            stateFile = MemoryMappedFile.CreateFromFile(stateFileName, FileMode.OpenOrCreate, null, StateFileSize, MemoryMappedFileAccess.ReadWrite);
            stateView = stateFile.CreateViewAccessor();
            position = stateView.ReadInt64(0L);
        }

        internal long Position => position;

        internal void Advance(long count = 1L) => stateView.Write(0L, position += count);

        public void Dispose()
        {
            stateView.Dispose();
            stateFile.Dispose();
            this = default;
        }
    }
}
