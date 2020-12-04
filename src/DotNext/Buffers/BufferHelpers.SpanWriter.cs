using System.IO;

namespace DotNext.Buffers
{
    public static partial class BufferHelpers
    {
        /// <summary>
        /// Writes value of blittable type as bytes to the underlying memory block.
        /// </summary>
        /// <param name="writer">The memory writer.</param>
        /// <param name="value">The value of blittable type.</param>
        /// <typeparam name="T">The blittable type.</typeparam>
        /// <returns>
        /// <see langword="true"/> if all bytes are copied successfully;
        /// <see langword="false"/> if remaining space in the underlying span is not enough to place all <paramref name="value"/> bytes.
        /// </returns>
        public static bool TryWrite<T>(this ref SpanWriter<byte> writer, in T value)
            where T : unmanaged
            => writer.TryWrite(Span.AsReadOnlyBytes(in value));

        /// <summary>
        /// Writes value of blittable type as bytes to the underlying memory block.
        /// </summary>
        /// <param name="writer">The memory writer.</param>
        /// <param name="value">The value of blittable type.</param>
        /// <typeparam name="T">The blittable type.</typeparam>
        /// <exception cref="EndOfStreamException">Remaining space in the underlying span is not enough to place all <paramref name="value"/> bytes.</exception>
        public static void Write<T>(this ref SpanWriter<byte> writer, in T value)
            where T : unmanaged
            => writer.Write(Span.AsReadOnlyBytes(in value));
    }
}