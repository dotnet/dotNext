using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Buffers;

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
    public static unsafe bool TryRead<T>(this scoped ref SpanReader<byte> reader, out T result)
        where T : unmanaged
    {
        if (MemoryMarshal.TryRead(reader.RemainingSpan, out result))
        {
            reader.Advance(sizeof(T));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads the value of blittable type from the raw bytes
    /// represents by memory block.
    /// </summary>
    /// <param name="reader">The memory reader.</param>
    /// <typeparam name="T">The blittable type.</typeparam>
    /// <returns>The value deserialized from bytes.</returns>
    /// <exception cref="InternalBufferOverflowException">The end of memory block is reached.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe T Read<T>(this ref SpanReader<byte> reader)
        where T : unmanaged
        => Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(reader.Read(sizeof(T))));

    /// <summary>
    /// Reads the value encoded in little-endian byte order.
    /// </summary>
    /// <typeparam name="T">The type of the value to decode.</typeparam>
    /// <param name="reader">The memory reader.</param>
    /// <param name="isUnsigned">
    /// <see langword="true"/> if source represents an unsigned two's complement number;
    /// otherwise, <see langword="false"/> to indicate it represents a signed two's complement number.
    /// </param>
    /// <returns>Decoded value.</returns>
    public static unsafe T ReadLittleEndian<T>(this ref SpanReader<byte> reader, bool isUnsigned)
        where T : unmanaged, IBinaryInteger<T>
        => T.ReadLittleEndian(reader.Read(sizeof(T)), isUnsigned);

    /// <summary>
    /// Reads the value encoded in big-endian byte order.
    /// </summary>
    /// <typeparam name="T">The type of the value to decode.</typeparam>
    /// <param name="reader">The memory reader.</param>
    /// <param name="isUnsigned">
    /// <see langword="true"/> if source represents an unsigned two's complement number;
    /// otherwise, <see langword="false"/> to indicate it represents a signed two's complement number.
    /// </param>
    /// <returns>Decoded value.</returns>
    public static unsafe T ReadBigEndian<T>(this ref SpanReader<byte> reader, bool isUnsigned)
        where T : unmanaged, IBinaryInteger<T>
        => T.ReadBigEndian(reader.Read(sizeof(T)), isUnsigned);
}