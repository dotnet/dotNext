using System;
using System.ComponentModel;
using System.Net;
using System.Text;

namespace DotNext.Net
{
    using Buffers;
    using IO;

    /// <summary>
    /// Provides methods for serialization/deserialization of <see cref="EndPoint"/> derived types.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class EndPointFormatter
    {
        private const int IPv6AddressSize = 16;

        private const byte IPEndPointPrefix = 1;
        private const byte DnsEndPointPrefix = 2;

        private static Encoding HostNameEncoding => Encoding.UTF8;

        /// <summary>
        /// Serializes <see cref="IPEndPoint"/> or <see cref="DnsEndPoint"/> to the array of bytes.
        /// </summary>
        /// <param name="writer">The output buffer.</param>
        /// <param name="endPoint">The value to be serialized.</param>
        /// <exception cref="ArgumentOutOfRangeException">Unsupported type of <paramref name="endPoint"/>.</exception>
        public static void WriteEndPoint(this ref BufferWriterSlim<byte> writer, EndPoint endPoint)
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
                    Serialize(ip.Address, ref writer);
                    break;
                case DnsEndPoint dns:
                    // the format is:
                    // DNS endpoint type = 1 byte
                    // port = 4 bytes
                    // host name length, N = 4 bytes
                    // host name = N bytes
                    writer.Add(DnsEndPointPrefix);
                    writer.WriteInt32(dns.Port, true);
                    Serialize(dns.Host, ref writer);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(endPoint));
            }
        }

        private static void Serialize(IPAddress address, ref BufferWriterSlim<byte> writer)
        {
            Span<byte> addressBytes = stackalloc byte[IPv6AddressSize];

            if (!address.TryWriteBytes(addressBytes, out var bytesWritten) || bytesWritten > byte.MaxValue)
                throw new NotSupportedException();

            writer.Add((byte)bytesWritten);
            addressBytes.Slice(0, bytesWritten).CopyTo(writer.GetSpan(bytesWritten));
            writer.Advance(bytesWritten);
        }

        private static void Serialize(ReadOnlySpan<char> hostName, ref BufferWriterSlim<byte> writer)
        {
            var count = HostNameEncoding.GetByteCount(hostName);
            writer.WriteInt32(count, true);

            HostNameEncoding.GetBytes(hostName, writer.GetSpan(count));
            writer.Advance(count);
        }

        /// <summary>
        /// Deserializes <see cref="IPEndPoint"/> or <see cref="DnsEndPoint"/>.
        /// </summary>
        /// <param name="reader">The binary reader.</param>
        /// <returns>The deserialized network endpoint address.</returns>
        public static EndPoint ReadEndPoint(this ref SequenceBinaryReader reader) => reader.Read<byte>() switch
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