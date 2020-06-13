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