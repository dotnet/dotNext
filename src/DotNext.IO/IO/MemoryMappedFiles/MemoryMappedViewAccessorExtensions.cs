using System.IO.MemoryMappedFiles;

namespace DotNext.IO.MemoryMappedFiles
{
    internal static class MemoryMappedViewAccessorExtensions
    {
        internal static void ReleasePointerAndDispose(this MemoryMappedViewAccessor accessor)
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            accessor.Dispose();
        }
    }
}
