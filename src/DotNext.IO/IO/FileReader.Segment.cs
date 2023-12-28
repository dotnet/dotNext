using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace DotNext.IO;

using Buffers;

public partial class FileReader
{
    private readonly struct SegmentLength(long? value) :
        IEquatable<SegmentLength>,
        IEquatable<long>,
        IComparisonOperators<SegmentLength, long, bool>,
        IEqualityOperators<SegmentLength, SegmentLength, bool>,
        IAdditionOperators<SegmentLength, long, SegmentLength>,
        ISubtractionOperators<SegmentLength, long, SegmentLength>
    {
        private readonly long value = value.GetValueOrDefault(-1L);

        public SegmentLength()
            : this(null)
        {
        }

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

        public override string ToString() => IsInfinite ? "Infinite" : value.ToString(CultureInfo.InvariantCulture);

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

    private SegmentLength length = new();

    /// <summary>
    /// Limits the number of available bytes to read.
    /// </summary>
    /// <remarks>
    /// This limit is applicable only to the methods of <see cref="IAsyncBinaryReader"/> interface
    /// implemented by this class.
    /// </remarks>
    /// <value>The number of available bytes to read; or <see langword="null"/> to allow read to the end of the file.</value>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public long? ReaderSegmentLength
    {
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value.GetValueOrDefault());

            length = new(value);
        }
    }

    private static ReadOnlyMemory<byte> TrimLength(ReadOnlyMemory<byte> buffer, SegmentLength length)
        => length.IsInfinite ? buffer : buffer.TrimLength(int.CreateSaturating((long)length));
}