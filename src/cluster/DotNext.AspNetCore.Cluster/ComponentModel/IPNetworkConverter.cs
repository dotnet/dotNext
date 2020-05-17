using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;

namespace DotNext.ComponentModel
{
    [SuppressMessage("Usage", "CA1812", Justification = "This class is instantiated implicitly via Register method")]
    internal sealed class IPNetworkConverter : TypeConverter
    {
        internal static void Register()
            => TypeDescriptor.AddAttributes(typeof(IPNetwork), new TypeConverterAttribute(typeof(IPNetworkConverter)));

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType == typeof(string);

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            switch (value)
            {
                case string network:
                    return IPNetwork.Parse(network);
                default:
                    throw new NotSupportedException();
            }
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            => destinationType == typeof(string);

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            switch (value)
            {
                case IPNetwork network:
                    return network.ToString();
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
