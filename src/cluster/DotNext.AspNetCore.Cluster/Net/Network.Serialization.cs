using System;
using System.Buffers;
using System.Net;
using System.Text;

namespace DotNext.Net
{
    using Buffers;
    using IO;

    // EndPoint serialization engine is primarily used by HyParView and SWIM membership protocols internally
    internal static partial class Network
    {
        private const int IPv6AddressSize = 16;

        private const byte IPEndPointPrefix = 1;
        private const byte DnsEndPointPrefix = 2;

        internal static void SerializeEndPoint(EndPoint endPoint, ref BufferWriterSlim<byte> writer)
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

            writer.Add((byte)bytesWritten);
            addressBytes.Slice(0, bytesWritten).CopyTo(writer.GetSpan(bytesWritten));
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

        internal static EndPoint DeserializeEndPoint(ref SequenceBinaryReader reader) => reader.Read<byte>() switch
        {
            IPEndPointPrefix => DeserializeIP(ref reader),
            DnsEndPointPrefix => DeserializeHost(ref reader),
            _ => throw new NotSupportedException(),
        };

        private static IPEndPoint DeserializeIP(ref SequenceBinaryReader reader)
        {
            var port = reader.ReadInt32(true);
            var bytesCount = reader.Read<byte>();

            Span<byte> bytes = stackalloc byte[bytesCount];
            reader.Read(bytes);

            return new IPEndPoint(new IPAddress(bytes), port);
        }

        private static DnsEndPoint DeserializeHost(ref SequenceBinaryReader reader)
        {
            var port = reader.ReadInt32(true);
            var length = reader.ReadInt32(true);

            string hostName;
            using (var hostNameBuffer = (uint)length <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[length] : new MemoryRental<byte>(length, true))
            {
                reader.Read(hostNameBuffer.Span);
                hostName = HostNameEncoding.GetString(hostNameBuffer.Span);
            }

            return new DnsEndPoint(hostName, port);
        }
    }
}