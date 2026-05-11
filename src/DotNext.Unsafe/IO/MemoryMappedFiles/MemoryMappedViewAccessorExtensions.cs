using System.IO.MemoryMappedFiles;

namespace DotNext.IO.MemoryMappedFiles;

using Runtime.InteropServices;

internal static class MemoryMappedViewAccessorExtensions
{
    extension(MemoryMappedViewAccessor accessor)
    {
        public FileAccess AccessMode => (accessor.CanRead, accessor.CanWrite) switch
        {
            (true, false) => FileAccess.Read,
            (false, true) => FileAccess.Write,
            (true, true) => FileAccess.ReadWrite,
            _ => default,
        };
        
        public Pointer<byte> Pointer
            => new(nint.CreateChecked(accessor.SafeMemoryMappedViewHandle.DangerousGetHandle() + accessor.PointerOffset));
    }
}