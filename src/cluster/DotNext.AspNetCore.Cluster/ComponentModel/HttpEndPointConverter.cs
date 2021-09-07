using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;

namespace DotNext.ComponentModel;

using HttpEndPoint = Net.HttpEndPoint;

[SuppressMessage("Performance", "CA1812", Justification = "This class is instantiated implicitly via Register method")]
internal sealed class HttpEndPointConverter : TypeConverter
{
    internal static void Register()
        => TypeDescriptor.AddAttributes(typeof(DnsEndPoint), new TypeConverterAttribute(typeof(HttpEndPointConverter)));

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType.IsOneOf(typeof(string));

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string str && HttpEndPoint.TryParse(str, out var result) ? result : throw new NotSupportedException();

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is HttpEndPoint http ? http.ToString() : throw new NotSupportedException();
}
