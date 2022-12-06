using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;

namespace DotNext.ComponentModel;

using Buffers;
using ClusterMemberId = Net.Cluster.ClusterMemberId;

[SuppressMessage("Performance", "CA1812", Justification = "This class is instantiated implicitly via Register method")]
internal sealed class ClusterMemberIdConverter : TypeConverter
{
    internal static void Register()
        => TypeDescriptor.AddAttributes(typeof(ClusterMemberId), new TypeConverterAttribute(typeof(ClusterMemberIdConverter)));

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType.IsOneOf(typeof(string), typeof(byte[]), typeof(Memory<byte>), typeof(ReadOnlyMemory<byte>), typeof(EndPoint));

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        ClusterMemberId result;

        switch (value)
        {
            case string hex:
                if (!ClusterMemberId.TryParse(hex, out result))
                    throw new FormatException();
                break;
            case byte[] binary:
                result = new(binary);
                break;
            case Memory<byte> binary:
                result = new(binary.Span);
                break;
            case ReadOnlyMemory<byte> binary:
                result = new(binary.Span);
                break;
            case EndPoint ep:
                result = ClusterMemberId.FromEndPoint(ep);
                break;
            default:
                throw new NotSupportedException();
        }

        return result;
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType?.IsOneOf(typeof(string), typeof(byte[])) ?? false;

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is ClusterMemberId id)
        {
            if (destinationType == typeof(string))
                return id.ToString();

            if (destinationType == typeof(byte[]))
            {
                var bytes = new byte[ClusterMemberId.Size];
                var writer = new SpanWriter<byte>(bytes);
                id.Format(ref writer);
                return bytes;
            }
        }

        throw new NotSupportedException();
    }
}