using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotNext.Net;

using Buffers;
using Buffers.Binary;
using Numerics;
using HttpEndPoint = Http.HttpEndPoint;

partial class EndPointFormatter
{
    private const int IPv6AddressSize = 16;

    private const byte IPEndPointPrefix = 1;
    private const byte DnsEndPointPrefix = 2;
    private const byte HttpEndPointPrefix = 3;
    private const byte DomainSocketEndPointPrefix = 4;
    private const byte UriEndPointPrefix = 5;

    private const int PrefixSize = sizeof(byte);
    private static Encoding HostNameEncoding => Encoding.UTF8;
    
    private static void WriteEndPoint<TWriter>(TWriter bufferWriter, EndPoint endPoint)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        switch (endPoint)
        {
            case null:
                throw new ArgumentNullException(nameof(endPoint));
            case IPEndPoint ip:
                bufferWriter.WriteIp(ip);
                return;
            case HttpEndPoint http:
                bufferWriter.WriteHttp(http);
                return;
            case DnsEndPoint dns:
                bufferWriter.WriteDns(dns);
                return;
            case UnixDomainSocketEndPoint domainSocket:
                bufferWriter.WriteUds(domainSocket);
                return;
        }

        switch (endPoint.GetType().FullName)
        {
            case UriEndPoint.TypeName:
                bufferWriter.WriteUri(endPoint);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(endPoint));
        }
    }

    extension<TWriter>(TWriter bufferWriter)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        private void WriteIp(IPEndPoint ip)
        {
            // the format is:
            // IP endpoint type = 1 byte
            // port = 4 bytes
            // number of address bytes, N = 1 byte
            // address bytes = N bytes
            var writer = new SpanWriter<byte>(bufferWriter.GetSpan(PrefixSize + sizeof(int)));
            writer += IPEndPointPrefix;
            writer.WriteLittleEndian(ip.Port);
            bufferWriter.Advance(writer.WrittenCount);
            bufferWriter.WriteAddress(ip.Address);
        }

        private void WriteHttp(HttpEndPoint endPoint)
        {
            // the format is:
            // DNS endpoint type = 1 byte
            // HTTPS (true/false) = 1 byte
            // port = 4 bytes
            // address family = 4 bytes
            // host name length, N = 4 bytes
            // host name = N bytes
            var writer = new SpanWriter<byte>(bufferWriter.GetSpan(PrefixSize + sizeof(byte) + sizeof(int) + sizeof(AddressFamily)));
            writer += HttpEndPointPrefix;
            writer += Unsafe.BitCast<bool, byte>(endPoint.IsSecure);
            writer.WriteLittleEndian(endPoint.Port);
            writer.WriteLittleEndian<Enum<AddressFamily>>(new(endPoint.AddressFamily));
            bufferWriter.Advance(writer.WrittenCount);
            bufferWriter.WriteHost(endPoint.Host);
        }

        private void WriteDns(DnsEndPoint endPoint)
        {
            // the format is:
            // DNS endpoint type = 1 byte
            // port = 4 bytes
            // address family = 4 bytes
            // host name length, N = 4 bytes
            // host name = N bytes
            var writer = new SpanWriter<byte>(bufferWriter.GetSpan(PrefixSize + sizeof(int) + sizeof(AddressFamily)));
            writer += DnsEndPointPrefix;
            writer.WriteLittleEndian(endPoint.Port);
            writer.WriteLittleEndian<Enum<AddressFamily>>(new(endPoint.AddressFamily));
            bufferWriter.Advance(writer.WrittenCount);
            bufferWriter.WriteHost(endPoint.Host);
        }

        private void WriteUds(UnixDomainSocketEndPoint endPoint)
        {
            // the format is:
            // UDS endpoint type = 1 byte
            // path name length, N = 4 bytes
            // path name = N bytes
            bufferWriter.GetSpan()[0] = DomainSocketEndPointPrefix;
            bufferWriter.Advance(PrefixSize);
            bufferWriter.WriteHost(endPoint.ToString());
        }

        private void WriteUri(EndPoint endPoint)
        {
            // the format is:
            // URI endpoint type = 1 byte
            // URI length, N = 4 bytes
            // URI = N bytes
            bufferWriter.GetSpan()[0] = UriEndPointPrefix;
            bufferWriter.Advance(PrefixSize);
            bufferWriter.WriteHost(UriEndPoint.GetUri(endPoint).ToString());
        }

        private void WriteAddress(IPAddress address)
        {
            var buffer = bufferWriter.GetSpan(IPv6AddressSize + 1);

            if (!address.TryWriteBytes(buffer[1..], out var bytesWritten))
                throw new NotSupportedException();

            buffer[0] = (byte)bytesWritten;
            bufferWriter.Advance(bytesWritten + 1);
        }

        private void WriteHost(ReadOnlySpan<char> hostName)
        {
            var count = HostNameEncoding.GetByteCount(hostName);
            var writer = new SpanWriter<byte>(bufferWriter.GetSpan(sizeof(int) + count));
            writer.WriteLittleEndian(count);
            writer.Advance(HostNameEncoding.GetBytes(hostName, writer.RemainingSpan));

            bufferWriter.Advance(writer.WrittenCount);
        }
    }

    extension(ref SequenceReader reader)
    {
        private UnixDomainSocketEndPoint ReadDomainSocket()
        {
            var length = reader.ReadLittleEndian<int>();

            using var pathBuffer = (uint)length <= (uint)SpanOwner<byte>.StackallocThreshold
                ? stackalloc byte[length]
                : new SpanOwner<byte>(length);

            reader.Read(pathBuffer.Span);
            return new(HostNameEncoding.GetString(pathBuffer.Span));
        }
        
        private EndPoint ReadUri()
        {
            var length = reader.ReadLittleEndian<int>();

            using var pathBuffer = (uint)length <= (uint)SpanOwner<byte>.StackallocThreshold
                ? stackalloc byte[length]
                : new SpanOwner<byte>(length);

            reader.Read(pathBuffer.Span);
            return UriEndPoint.Create(new Uri(HostNameEncoding.GetString(pathBuffer.Span), UriKind.Absolute));
        }
        
        [SkipLocalsInit]
        private IPEndPoint ReadIp()
        {
            var port = reader.ReadLittleEndian<int>();
            var bytesCount = reader.ReadByte();

            Span<byte> bytes = stackalloc byte[bytesCount];
            reader.Read(bytes);

            return new(new IPAddress(bytes), port);
        }

        private DnsEndPoint ReadDns()
            => new(reader.ReadHost(out var port, out var family), port, family);

        private HttpEndPoint ReadHttp()
        {
            var secure = Unsafe.BitCast<byte, bool>(reader.ReadByte());
            return new(reader.ReadHost(out var port, out var family), port, secure, family);
        }
        
        private string ReadHost(out int port, out AddressFamily family)
        {
            port = reader.ReadLittleEndian<int>();
            family = reader.ReadLittleEndian<Enum<AddressFamily>>();
            var length = reader.ReadLittleEndian<int>();

            using var hostNameBuffer = (uint)length <= (uint)SpanOwner<byte>.StackallocThreshold
                ? stackalloc byte[length]
                : new SpanOwner<byte>(length);
            reader.Read(hostNameBuffer.Span);
            return HostNameEncoding.GetString(hostNameBuffer.Span);
        }
    }
}

file static class UriEndPoint
{
    public const string TypeName = "Microsoft.AspNetCore.Connections.UriEndPoint";
    private const string FullyQualifiedName = $"{TypeName}, Microsoft.AspNetCore.Connections.Abstractions";
    
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Uri")]
    private static extern Uri GetUriUnsafe([UnsafeAccessorType(FullyQualifiedName)] object endPoint);

    public static Uri GetUri(EndPoint endPoint) => GetUriUnsafe(endPoint);
    
    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType(FullyQualifiedName)]
    private static extern object CreateUnsafe(Uri uri);

    public static EndPoint Create(Uri uri) => (EndPoint)CreateUnsafe(uri);
}