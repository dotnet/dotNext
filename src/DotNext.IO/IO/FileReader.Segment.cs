using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace DotNext.IO;

using Buffers;

public partial class FileReader
{
    private readonly struct SegmentLength : IEquatable<SegmentLength>, IEquatable<long>
    {
        private readonly long value;

        private SegmentLength(long value)
            => this.value = value;

        internal SegmentLength(long? value)
        {
            if (value is null)
            {
                this = Infinite;
            }
            else if (value.GetValueOrDefault() < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            else
            {
                this.value = value.GetValueOrDefault();
            }
        }

        internal static SegmentLength Infinite => new(-1L);

        internal bool IsInfinite => value < 0L;

        public bool Equals(long other) => !IsInfinite && value == other;

        public bool Equals(SegmentLength other)
            => IsInfinite ? other.IsInfinite : other.Equals(value);

        public override bool Equals([NotNullWhen(true)] object? other) => other switch
        {
            SegmentLength length => Equals(length),
            IConvertible length => Equals(length.ToInt64(CultureInfo.InvariantCulture)),
            _ => false,
        };

        public override int GetHashCode() => value.GetHashCode();

        public static bool operator >(SegmentLength x, long y)
            => x.IsInfinite || x.value > y;

        public static bool operator >=(SegmentLength x, long y)
            => x.IsInfinite || x.value >= y;

        public static bool operator <(SegmentLength x, long y)
            => !x.IsInfinite && x.value < y;

        public static bool operator <=(SegmentLength x, long y)
            => !x.IsInfinite && x.value <= y;

        public static bool operator ==(SegmentLength x, long y)
            => x.Equals(y);

        public static bool operator ==(SegmentLength x, SegmentLength y)
            => x.Equals(y);

        public static bool operator !=(SegmentLength x, SegmentLength y)
            => !x.Equals(y);

        public static bool operator !=(SegmentLength x, long y)
            => !x.Equals(y);

        public static SegmentLength operator +(SegmentLength x, long y)
            => x.IsInfinite ? x : new(x.value + y);

        public static SegmentLength operator -(SegmentLength x, long y)
            => x.IsInfinite ? x : new(x.value - y);

        public static explicit operator long(SegmentLength x) => x.value;
    }

    private SegmentLength length = SegmentLength.Infinite;

    /// <summary>
    /// Limits the number of available bytes to read.
    /// </summary>
    /// <remarks>
    /// This limit is applicable only to the methods of <see cref="IAsyncBinaryReader"/> interface
    /// implemented by this class.
    /// </remarks>
    /// <param name="value">The number of available bytes to read; or <see langword="null"/> to allow read to the end of the file.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public void SetSegmentLength(long? value) => length = new(value);

    private static ReadOnlyMemory<byte> TrimLength(ReadOnlyMemory<byte> buffer, SegmentLength length)
        => length.IsInfinite ? buffer : buffer.TrimLength(ValueTypeExtensions.Truncate((long)length));

    private static Memory<byte> TrimLength(Memory<byte> buffer, SegmentLength length)
        => length.IsInfinite ? buffer : buffer.TrimLength(ValueTypeExtensions.Truncate((long)length));
}