using System;
using System.ComponentModel;
using System.Globalization;
using System.Net;

namespace DotNext.ComponentModel
{
    internal sealed class IPAddressConverter : TypeConverter
    {
        internal static void Register()
            => TypeDescriptor.AddAttributes(typeof(IPAddress), new TypeConverterAttribute(typeof(IPAddressConverter)));

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType == typeof(string);

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            switch (value)
            {
                case string address:
                    return IPAddress.Parse(address);
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
                case IPAddress address:
                    return address.ToString();
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
