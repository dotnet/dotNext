using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace DotNext.Buffers.Binary;

using Number = Numerics.Number;

/// <summary>
/// Represents binary integer in big-endian format.
/// </summary>
/// <typeparam name="T">The type of binary integer.</typeparam>
public struct BigEndian<T> : IBinaryFormattable<BigEndian<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// A value that is represented in big-endian format.
    /// </summary>
    required public T Value;

    /// <inheritdoc cref="IBinaryFormattable{T}.Size"/>
    static int IBinaryFormattable<BigEndian<T>>.Size => Number.GetMaxByteCount<T>();

    /// <summary>
    /// Encodes <see cref="Value"/> as a sequence of bytes in big-endian format.
    /// </summary>
    /// <param name="output">The output buffer.</param>
    /// <exception cref="ArgumentException"><paramref name="output"/> is not enough to place <see cref="Value"/> in big-endian format.</exception>
    void IBinaryFormattable<BigEndian<T>>.Format(Span<byte> output)
    {
        if (!Value.TryWriteBigEndian(output, out _))
            ThrowArgumentException();

        [DoesNotReturn]
        [StackTraceHidden]
        static void ThrowArgumentException()
            => throw new ArgumentException(ExceptionMessages.SmallBuffer, nameof(output));
    }

    /// <summary>
    /// Decodes <see cref="Value"/> from big-endian format.
    /// </summary>
    /// <param name="input">The input buffer containing <see cref="Value"/> in big-endian format.</param>
    /// <returns>A value restored from big-endian format.</returns>
    static BigEndian<T> IBinaryFormattable<BigEndian<T>>.Parse(ReadOnlySpan<byte> input)
        => new() { Value = T.ReadBigEndian(input, Number.IsSigned<T>() is false) };

    /// <summary>
    /// Converts a value from big-endian format.
    /// </summary>
    /// <param name="le">A value encoded as big-endian.</param>
    public static implicit operator T(BigEndian<T> le) => le.Value;
}