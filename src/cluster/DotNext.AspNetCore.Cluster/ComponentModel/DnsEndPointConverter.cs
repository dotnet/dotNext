using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;

namespace DotNext.ComponentModel
{
    [SuppressMessage("Performance", "CA1812", Justification = "This class is instantiated implicitly via Register method")]
    internal sealed class DnsEndPointConverter : TypeConverter
    {
        internal static void Register()
            => TypeDescriptor.AddAttributes(typeof(DnsEndPoint), new TypeConverterAttribute(typeof(DnsEndPointConverter)));

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            => sourceType.IsOneOf(typeof(string), typeof(ReadOnlyMemory<char>), typeof(Memory<char>), typeof(char[]));

        private static DnsEndPoint Parse(ReadOnlySpan<char> endPoint)
        {
            var index = endPoint.IndexOf(':');
            if (index < 0)
                throw new FormatException(ExceptionMessages.InvalidDnsEndPointFormat);

            var hostName = new string(endPoint[..index]);
            var port = int.Parse(endPoint[index..], provider: CultureInfo.InvariantCulture);

            return new DnsEndPoint(hostName, port);
        }

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
            => value is IPAddress ip ? ip.ToString() : throw new NotSupportedException();
    }
}
