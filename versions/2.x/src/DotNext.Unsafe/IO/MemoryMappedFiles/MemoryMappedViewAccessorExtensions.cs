using System.IO;
using System.IO.MemoryMappedFiles;

namespace DotNext.IO.MemoryMappedFiles
{
    using Intrinsics = Runtime.Intrinsics;

    internal static class MemoryMappedViewAccessorExtensions
    {
        internal static FileAccess GetFileAccess(this MemoryMappedViewAccessor accessor)
            => Intrinsics.ToInt32(accessor.CanRead, accessor.CanWrite) switch
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
