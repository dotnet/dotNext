using System.IO;
using System.IO.MemoryMappedFiles;

namespace DotNext.IO.MemoryMappedFiles
{
    using static Numerics.BitVector;

    internal static class MemoryMappedViewAccessorExtensions
    {
        internal static FileAccess GetFileAccess(this MemoryMappedViewAccessor accessor)
            => ToByte(stackalloc bool[] { accessor.CanRead, accessor.CanWrite }) switch
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
