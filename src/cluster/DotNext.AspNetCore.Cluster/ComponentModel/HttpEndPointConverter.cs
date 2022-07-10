using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;

namespace DotNext.ComponentModel;

using HttpEndPoint = Net.Http.HttpEndPoint;

[SuppressMessage("Performance", "CA1812", Justification = "This class is instantiated implicitly via Register method")]
internal sealed class HttpEndPointConverter : TypeConverter
{
    internal static void Register()
    {
        var converter = new TypeConverterAttribute(typeof(HttpEndPointConverter));
        TypeDescriptor.AddAttributes(typeof(DnsEndPoint), converter);
        TypeDescriptor.AddAttributes(typeof(HttpEndPoint), converter);
    }

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType.IsOneOf(typeof(string));

    public override HttpEndPoint? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string str && HttpEndPoint.TryParse(str, out var result) ? result : throw new NotSupportedException();

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is HttpEndPoint http ? http.ToString() : throw new NotSupportedException();
}
