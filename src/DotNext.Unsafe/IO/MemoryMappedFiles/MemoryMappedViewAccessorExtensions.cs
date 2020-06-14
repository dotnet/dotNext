using System.IO;
using System.IO.MemoryMappedFiles;

namespace DotNext.IO.MemoryMappedFiles
{
    internal static class MemoryMappedViewAccessorExtensions
    {
        internal static FileAccess GetFileAccess(this MemoryMappedViewAccessor accessor)
            => (accessor.CanRead.ToInt32() + (accessor.CanWrite.ToInt32() << 1)) switch
            {
                1 => FileAccess.Read,
                2 => FileAccess.Write,
                3 => FileAccess.ReadWrite,
                _ => default,
            };

        internal static void ReleasePointerAndDispose(this MemoryMappedViewAccessor accessor)
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            accessor.Dispose();
        }
    }
}
