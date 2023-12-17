using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace DotNext.Buffers.Binary;

using Number = Numerics.Number;

/// <summary>
/// Represents binary integer in little-endian format.
/// </summary>
/// <typeparam name="T">The type of binary integer.</typeparam>
public struct LittleEndian<T> : IBinaryFormattable<LittleEndian<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// A value that is represented in little-endian format.
    /// </summary>
    required public T Value;

    /// <inheritdoc cref="IBinaryFormattable{T}.Size"/>
    static int IBinaryFormattable<LittleEndian<T>>.Size => Number.GetMaxByteCount<T>();

    /// <summary>
    /// Encodes <see cref="Value"/> as a sequence of bytes in little-endian format.
    /// </summary>
    /// <param name="output">The output buffer.</param>
    /// <exception cref="ArgumentException"><paramref name="output"/> is not enough to place <see cref="Value"/> in little-endian format.</exception>
    void IBinaryFormattable<LittleEndian<T>>.Format(Span<byte> output)
    {
        if (!Value.TryWriteLittleEndian(output, out _))
            ThrowArgumentException();

        [DoesNotReturn]
        [StackTraceHidden]
        static void ThrowArgumentException()
            => throw new ArgumentException(ExceptionMessages.SmallBuffer, nameof(output));
    }

    /// <summary>
    /// Decodes <see cref="Value"/> from little-endian format.
    /// </summary>
    /// <param name="input">The input buffer containing <see cref="Value"/> in little-endian format.</param>
    /// <returns>A value restored from little-endian format.</returns>
    static LittleEndian<T> IBinaryFormattable<LittleEndian<T>>.Parse(ReadOnlySpan<byte> input)
        => new() { Value = T.ReadLittleEndian(input, Number.IsSigned<T>() is false) };

    /// <summary>
    /// Converts a value from little-endian format.
    /// </summary>
    /// <param name="le">A value encoded as little-endian.</param>
    public static implicit operator T(LittleEndian<T> le) => le.Value;
}