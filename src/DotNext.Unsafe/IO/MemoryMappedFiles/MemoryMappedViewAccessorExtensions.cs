using System.IO.MemoryMappedFiles;

namespace DotNext.IO.MemoryMappedFiles;

internal static class MemoryMappedViewAccessorExtensions
{
    internal static FileAccess GetFileAccess(this MemoryMappedViewAccessor accessor)
        => (accessor.CanRead, accessor.CanWrite) switch
        {
            (true, false) => FileAccess.Read,
            (false, true) => FileAccess.Write,
            (true, true) => FileAccess.ReadWrite,
            _ => default,
        };

    internal static void ReleasePointerAndDispose(this MemoryMappedViewAccessor accessor)
    {
        accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        accessor.Dispose();
    }
}