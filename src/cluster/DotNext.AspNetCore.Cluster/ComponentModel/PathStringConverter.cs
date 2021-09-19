using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace DotNext.ComponentModel;

[SuppressMessage("Performance", "CA1812", Justification = "This class is instantiated implicitly via Register method")]
internal sealed class PathStringConverter : TypeConverter
{
    internal static void Register()
        => TypeDescriptor.AddAttributes(typeof(PathString), new TypeConverterAttribute(typeof(PathStringConverter)));

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string path ? new PathString(path) : throw new NotSupportedException();

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is PathString path ? path.Value! : throw new NotSupportedException();
}