using System.IO.MemoryMappedFiles;

namespace DotNext.IO.MemoryMappedFiles
{
    /// <summary>
    /// Represents various extensions for <see cref="MemoryMappedFile"/> class.
    /// </summary>
    public static class MemoryMappedFileExtensions
    {
        /// <summary>
        /// Creates direct accessor the virtual memory associated with the memory-mapped file.
        /// </summary>
        /// <param name="file">The memory-mapped file.</param>
        /// <param name="offset">The byte at which to start the view.</param>
        /// <param name="size">The size of the view. Specify 0 (zero) to create a view that starts at offset and ends approximately at the end of the memory-mapped file.</param>
        /// <param name="access">the type of access allowed to the memory-mapped file.</param>
        /// <returns>The direct accessor.</returns>
        public static MemoryMappedDirectAccessor CreateDirectAccessor(this MemoryMappedFile file, long offset = 0, long size = 0, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite)
            => new MemoryMappedDirectAccessor(file.CreateViewAccessor(offset, size, access));
    }
}