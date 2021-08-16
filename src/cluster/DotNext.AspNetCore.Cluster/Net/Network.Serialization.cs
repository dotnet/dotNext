using System;
using System.Net;
using System.Text;

namespace DotNext.Net
{
    using Buffers;

    // EndPoint serialization engine is primarily used by HyParView and SWIM membership protocols internally
    internal static partial class Network
    {
        private const int IPv6AddressSize = 16;

        private const byte IPEndPointPrefix = 1;
        private const byte DnsEndPointPrefix = 2;

        internal static void Serialize(this EndPoint endPoint, ref BufferWriterSlim<byte> writer)
        {
            switch (endPoint)
            {
                case IPEndPoint ip:
                    // the format is:
                    // IP endpoint type = 1 byte
                    // port = 4 bytes
                    // number of address bytes, N = 1 byte
                    // address bytes = N bytes
                    writer.Add(IPEndPointPrefix);
                    writer.WriteInt32(ip.Port, true);
                    SerializeIP(ip.Address, ref writer);
                    break;
                case DnsEndPoint dns:
                    // the format is:
                    // DNS endpoint type = 1 byte
                    // port = 4 bytes
                    // host name length, N = 4 bytes
                    // host name = N bytes
                    writer.Add(DnsEndPointPrefix);
                    writer.WriteInt32(dns.Port, true);
                    SerializeHost(dns.Host, ref writer);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        private static void SerializeIP(IPAddress address, ref BufferWriterSlim<byte> writer)
        {
            Span<byte> addressBytes = stackalloc byte[IPv6AddressSize];

            if (!address.TryWriteBytes(addressBytes, out var bytesWritten) || bytesWritten > byte.MaxValue)
                throw new NotSupportedException();

            addressBytes = addressBytes.Slice(0, bytesWritten);
            writer.Add((byte)bytesWritten);

            addressBytes.CopyTo(writer.GetSpan(bytesWritten));
            writer.Advance(bytesWritten);
        }

        private static Encoding HostNameEncoding => Encoding.UTF8;

        private static void SerializeHost(ReadOnlySpan<char> hostName, ref BufferWriterSlim<byte> writer)
        {
            var count = HostNameEncoding.GetByteCount(hostName);
            writer.WriteInt32(count, true);

            HostNameEncoding.GetBytes(hostName, writer.GetSpan(count));
            writer.Advance(count);
        }

        internal static EndPoint Deserialize(ref SpanReader<byte> reader) => reader.Read() switch
        {
            IPEndPointPrefix => DeserializeIP(ref reader),
            DnsEndPointPrefix => DeserializeHost(ref reader),
            _ => throw new NotSupportedException(),
        };

        private static IPEndPoint DeserializeIP(ref SpanReader<byte> reader)
        {
            var port = reader.ReadInt32(true);
            var address = reader.Read(reader.Read());

            return new IPEndPoint(new IPAddress(address), port);
        }

        private static DnsEndPoint DeserializeHost(ref SpanReader<byte> reader)
        {
            var port = reader.ReadInt32(true);
            var hostName = HostNameEncoding.GetString(reader.Read(reader.ReadInt32(true)));

            return new DnsEndPoint(hostName, port);
        }
    }
}