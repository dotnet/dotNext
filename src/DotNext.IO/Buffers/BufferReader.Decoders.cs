using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    internal static partial class BufferReader
    {
        internal interface ISpanDecoder<T>
            where T : struct
        {
            T Decode(ReadOnlySpan<char> value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct NumberDecoder : ISpanDecoder<byte>, ISpanDecoder<sbyte>,
            ISpanDecoder<short>, ISpanDecoder<ushort>,
            ISpanDecoder<int>, ISpanDecoder<uint>,
            ISpanDecoder<long>, ISpanDecoder<ulong>,
            ISpanDecoder<decimal>,
            ISpanDecoder<float>, ISpanDecoder<double>,
            ISpanDecoder<BigInteger>
        {
            private readonly NumberStyles style;
            private readonly IFormatProvider? provider;

            internal NumberDecoder(NumberStyles style, IFormatProvider? provider)
            {
                this.style = style;
                this.provider = provider;
            }

            byte ISpanDecoder<byte>.Decode(ReadOnlySpan<char> value) => byte.Parse(value, style, provider);

            sbyte ISpanDecoder<sbyte>.Decode(ReadOnlySpan<char> value) => sbyte.Parse(value, style, provider);

            short ISpanDecoder<short>.Decode(ReadOnlySpan<char> value) => short.Parse(value, style, provider);

            ushort ISpanDecoder<ushort>.Decode(ReadOnlySpan<char> value) => ushort.Parse(value, style, provider);

            int ISpanDecoder<int>.Decode(ReadOnlySpan<char> value) => int.Parse(value, style, provider);

            uint ISpanDecoder<uint>.Decode(ReadOnlySpan<char> value) => uint.Parse(value, style, provider);

            long ISpanDecoder<long>.Decode(ReadOnlySpan<char> value) => long.Parse(value, style, provider);

            ulong ISpanDecoder<ulong>.Decode(ReadOnlySpan<char> value) => ulong.Parse(value, style, provider);

            float ISpanDecoder<float>.Decode(ReadOnlySpan<char> value) => float.Parse(value, style, provider);

            double ISpanDecoder<double>.Decode(ReadOnlySpan<char> value) => double.Parse(value, style, provider);

            decimal ISpanDecoder<decimal>.Decode(ReadOnlySpan<char> value) => decimal.Parse(value, style, provider);

            BigInteger ISpanDecoder<BigInteger>.Decode(ReadOnlySpan<char> value) => BigInteger.Parse(value, style, provider);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct DateTimeDecoder : ISpanDecoder<DateTime>, ISpanDecoder<DateTimeOffset>
        {
            private readonly DateTimeStyles style;
            private readonly IFormatProvider? provider;
            private readonly string[]? formats;

            internal DateTimeDecoder(DateTimeStyles style, IFormatProvider? provider)
            {
                formats = null;
                this.style = style;
                this.provider = provider;
            }

            internal DateTimeDecoder(DateTimeStyles style, string[] formats, IFormatProvider? provider)
            {
                this.formats = formats;
                this.style = style;
                this.provider = provider;
            }

            DateTime ISpanDecoder<DateTime>.Decode(ReadOnlySpan<char> value)
                => formats.IsNullOrEmpty() ? DateTime.Parse(value, provider, style) : DateTime.ParseExact(value, formats, provider, style);

            DateTimeOffset ISpanDecoder<DateTimeOffset>.Decode(ReadOnlySpan<char> value)
                => formats.IsNullOrEmpty() ? DateTimeOffset.Parse(value, provider, style) : DateTimeOffset.ParseExact(value, formats, provider, style);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct GuidDecoder : ISpanDecoder<Guid>
        {
            private readonly string? format;

            internal GuidDecoder(string format) => this.format = format;

            Guid ISpanDecoder<Guid>.Decode(ReadOnlySpan<char> value)
                => string.IsNullOrEmpty(format) ? Guid.Parse(value) : Guid.ParseExact(value, format);
        }

        internal readonly struct TimeSpanDecoder : ISpanDecoder<TimeSpan>
        {
            private readonly TimeSpanStyles style;
            private readonly IFormatProvider? provider;
            private readonly string[]? formats;

            internal TimeSpanDecoder(IFormatProvider? provider)
            {
                style = TimeSpanStyles.None;
                this.provider = provider;
                formats = null;
            }

            internal TimeSpanDecoder(TimeSpanStyles style, string[] formats, IFormatProvider? provider)
            {
                this.style = style;
                this.provider = provider;
                this.formats = formats;
            }

            TimeSpan ISpanDecoder<TimeSpan>.Decode(ReadOnlySpan<char> value)
                => formats.IsNullOrEmpty() ? TimeSpan.Parse(value, provider) : TimeSpan.ParseExact(value, formats, provider, style);
        }
    }
}