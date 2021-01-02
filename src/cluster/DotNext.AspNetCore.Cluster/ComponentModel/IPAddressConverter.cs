using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;

namespace DotNext.ComponentModel
{
    [SuppressMessage("Performance", "CA1812", Justification = "This class is instantiated implicitly via Register method")]
    internal sealed class IPAddressConverter : TypeConverter
    {
        internal static void Register()
            => TypeDescriptor.AddAttributes(typeof(IPAddress), new TypeConverterAttribute(typeof(IPAddressConverter)));

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType.IsOneOf(typeof(string), typeof(ReadOnlyMemory<char>), typeof(Memory<char>), typeof(char[]));

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) => value switch
        {
            string address => IPAddress.Parse(address),
            ReadOnlyMemory<char> memory => IPAddress.Parse(memory.Span),
            Memory<char> memory => IPAddress.Parse(memory.Span),
            char[] array => IPAddress.Parse(new ReadOnlySpan<char>(array)),
            _ => new NotSupportedException()
        };

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
