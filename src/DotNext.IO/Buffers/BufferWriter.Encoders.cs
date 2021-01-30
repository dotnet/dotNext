using System;
using System.Numerics;

namespace DotNext.Buffers
{
    public static partial class BufferWriter
    {
        internal static bool TryFormat(in sbyte value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format, provider);

        internal static bool TryFormat(in byte value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format, provider);

        internal static bool TryFormat(in short value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format, provider);

        internal static bool TryFormat(in ushort value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format, provider);

        internal static bool TryFormat(in int value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format, provider);

        internal static bool TryFormat(in uint value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format, provider);

        internal static bool TryFormat(in long value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format, provider);

        internal static bool TryFormat(in ulong value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format, provider);

        internal static bool TryFormat(in decimal value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format, provider);

        internal static bool TryFormat(in float value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format, provider);

        internal static bool TryFormat(in double value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format, provider);

        internal static bool TryFormat(in Guid value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format);

        internal static bool TryFormat(in DateTime value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format, provider);

        internal static bool TryFormat(in DateTimeOffset value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format, provider);

        internal static bool TryFormat(in TimeSpan value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format, provider);

        internal static bool TryFormat(in BigInteger value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            => value.TryFormat(destination, out charsWritten, format, provider);
    }
}