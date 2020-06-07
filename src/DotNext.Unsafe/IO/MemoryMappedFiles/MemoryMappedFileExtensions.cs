using System;
using System.IO.MemoryMappedFiles;

namespace DotNext.IO.MemoryMappedFiles
{
    /// <summary>
    /// Represents various extensions for <see cref="MemoryMappedFile"/> class.
    /// </summary>
    public static class MemoryMappedFileExtensions
    {
        /// <summary>
        /// Creates direct accessor to the virtual memory associated with the memory-mapped file.
        /// </summary>
        /// <param name="file">The memory-mapped file.</param>
        /// <param name="offset">The byte at which to start the view.</param>
        /// <param name="size">The size of the view. Specify 0 (zero) to create a view that starts at offset and ends approximately at the end of the memory-mapped file.</param>
        /// <param name="access">the type of access allowed to the memory-mapped file.</param>
        /// <returns>The direct accessor.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> or <paramref name="size"/> is less than zero.</exception>
        public static MemoryMappedDirectAccessor CreateDirectAccessor(this MemoryMappedFile file, long offset = 0, long size = 0, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite)
        {
            if (offset < 0L)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (size < 0L)
                throw new ArgumentOutOfRangeException(nameof(size));

            return new MemoryMappedDirectAccessor(file.CreateViewAccessor(offset, size, access));
        }

        /// <summary>
        /// Creates memory accessor to the virtual memory associated with the memory-mapped file.
        /// </summary>
        /// <remarks>
        /// This method is suitable if you need to represent memory-mapped file segment as <see cref="System.Memory{T}"/>.
        /// </remarks>
        /// <param name="file">The memory-mapped file.</param>
        /// <param name="offset">The byte at which to start the view.</param>
        /// <param name="size">The size of the view. Specify 0 (zero) to create a view that starts at offset and ends approximately at the end of the memory-mapped file.</param>
        /// <param name="access">the type of access allowed to the memory-mapped file.</param>
        /// <returns>The direct accessor.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> or <paramref name="size"/> is less than zero.</exception>
        public static IMappedMemoryOwner CreateMemoryAccessor(this MemoryMappedFile file, long offset = 0, int size = 0, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite)
        {
            if (offset < 0L)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (size < 0)
                throw new ArgumentOutOfRangeException(nameof(size));

            return new MappedMemoryOwner(file.CreateViewAccessor(offset, size, access));
        }

        /// <summary>
        /// Creates an accessor over memory-mapped file segments represented
        /// as <see cref="System.Buffers.ReadOnlySequence{T}"/>.
        /// </summary>
        /// <param name="file">The memory-mapped file.</param>
        /// <param name="segmentSize">
        /// The size of single segment, in bytes, that can be returned by <see cref="System.Buffers.ReadOnlySequence{T}"/>
        /// as contiguous block of memory. So this parameter defines actual amount of occupied virtual memory.
        /// </param>
        /// <param name="size">The observable length, in bytes, of memory-mapped file.</param>
        /// <returns>The object providing access to memory-mapped file via <see cref="System.Buffers.ReadOnlySequence{T}"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="segmentSize"/> is less than or equal to zero;
        /// or <paramref name="size"/> is less than or equal to zero;
        /// or <paramref name="segmentSize"/> is greater than <paramref name="size"/>.
        /// </exception>
        public static ReadOnlySequenceAccessor CreateSequenceAccessor(this MemoryMappedFile file, int segmentSize, long size)
        {
            if (segmentSize <= 0 || segmentSize > size)
                throw new ArgumentOutOfRangeException(nameof(segmentSize));
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size));

            return new ReadOnlySequenceAccessor(file, segmentSize, size);
        }
    }
}