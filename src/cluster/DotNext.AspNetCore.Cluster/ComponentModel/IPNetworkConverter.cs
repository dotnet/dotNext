using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;

namespace DotNext.ComponentModel
{
    [SuppressMessage("Performance", "CA1812", Justification = "This class is instantiated implicitly via Register method")]
    internal sealed class IPNetworkConverter : TypeConverter
    {
        internal static void Register()
            => TypeDescriptor.AddAttributes(typeof(IPNetwork), new TypeConverterAttribute(typeof(IPNetworkConverter)));

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType == typeof(string);

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            => value is string network ? IPNetwork.Parse(network) : throw new NotSupportedException();

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            => destinationType == typeof(string);

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
            => value is IPNetwork network ? network.ToString() : throw new NotSupportedException();
    }
}
