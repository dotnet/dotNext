using System.IO;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    public static partial class BufferHelpers
    {
        /// <summary>
        /// Reads the value of blittable type from the raw bytes
        /// represents by memory block.
        /// </summary>
        /// <param name="reader">The memory reader.</param>
        /// <param name="result">The value deserialized from bytes.</param>
        /// <typeparam name="T">The blittable type.</typeparam>
        /// <returns>
        /// <see langword="true"/> if memory block contains enough amount of unread bytes to decode the value;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        public static unsafe bool TryRead<T>(this ref SpanReader<byte> reader, out T result)
            where T : unmanaged
        {
            if (reader.TryRead(sizeof(T), out var block))
                return MemoryMarshal.TryRead(block, out result);

            result = default;
            return false;
        }

        /// <summary>
        /// Reads the value of blittable type from the raw bytes
        /// represents by memory block.
        /// </summary>
        /// <param name="reader">The memory reader.</param>
        /// <typeparam name="T">The blittable type.</typeparam>
        /// <returns>The value deserialized from bytes.</returns>
        /// <exception cref="EndOfStreamException">The end of memory block is reached.</exception>
        public static unsafe T Read<T>(this ref SpanReader<byte> reader)
            where T : unmanaged
            => MemoryMarshal.Read<T>(reader.Read(sizeof(T)));
    }
}