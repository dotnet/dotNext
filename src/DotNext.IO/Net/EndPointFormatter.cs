using System.Buffers;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotNext.Net;

using Buffers;
using Buffers.Binary;
using Numerics;
using HttpEndPoint = Http.HttpEndPoint;

/// <summary>
/// Provides methods for serialization/deserialization of <see cref="EndPoint"/> derived types.
/// </summary>
/// <remarks>
/// List of supported endpoint types: <see cref="IPEndPoint"/>, <see cref="DnsEndPoint"/>,
/// <see cref="HttpEndPoint"/>, <see cref="UnixDomainSocketEndPoint"/>, <c>UriEndPoint</c>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class EndPointFormatter
{
    private const int IPv6AddressSize = 16;

    private const byte IPEndPointPrefix = 1;
    private const byte DnsEndPointPrefix = 2;
    private const byte HttpEndPointPrefix = 3;
    private const byte DomainSocketEndPointPrefix = 4;
    private const byte UriEndPointPrefix = 5;

    private static Encoding HostNameEncoding => Encoding.UTF8;

    /// <summary>
    /// Serializes endpoint address to the buffer.
    /// </summary>
    /// <param name="endPoint">The value to be serialized.</param>
    /// <param name="allocator">The buffer allocator.</param>
    /// <returns>The buffer containing serialized <paramref name="endPoint"/>.</returns>
    public static MemoryOwner<byte> GetBytes(this EndPoint endPoint, MemoryAllocator<byte>? allocator = null)
    {
        MemoryOwner<byte> result;
        var writer = new BufferWriterSlim<byte>(128, allocator);

        try
        {
            WriteEndPoint(ref writer, endPoint);

            result = writer.DetachOrCopyBuffer();
        }
        finally
        {
            writer.Dispose();
        }

        return result;
    }

    /// <summary>
    /// Serializes endpoint address to the buffer.
    /// </summary>
    /// <param name="writer">The output buffer.</param>
    /// <param name="endPoint">The value to be serialized.</param>
    /// <exception cref="ArgumentOutOfRangeException">Unsupported type of <paramref name="endPoint"/>.</exception>
    public static void WriteEndPoint(this ref BufferWriterSlim<byte> writer, EndPoint endPoint)
        => WriteEndPoint<BufferWriterSlim<byte>.Ref>(new(ref writer), endPoint);

    /// <summary>
    /// Serializes endpoint address to the buffer.
    /// </summary>
    /// <param name="writer">The output buffer.</param>
    /// <param name="endPoint">The value to be serialized.</param>
    /// <exception cref="ArgumentOutOfRangeException">Unsupported type of <paramref name="endPoint"/>.</exception>
    public static void WriteEndPoint(this IBufferWriter<byte> writer, EndPoint endPoint)
        => WriteEndPoint<IBufferWriter<byte>>(writer, endPoint);

    private static void WriteEndPoint<TWriter>(TWriter bufferWriter, EndPoint endPoint)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        const int prefixSize = sizeof(byte);
        switch (endPoint)
        {
            case null:
                throw new ArgumentNullException(nameof(endPoint));
            case IPEndPoint ip:
                WriteIp(bufferWriter, ip);
                return;
            case HttpEndPoint http:
                WriteHttp(bufferWriter, http);
                return;
            case DnsEndPoint dns:
                WriteDns(bufferWriter, dns);
                return;
            case UnixDomainSocketEndPoint domainSocket:
                WriteUds(bufferWriter, domainSocket);
                return;
        }

        switch (endPoint.GetType().FullName)
        {
            case UriEndPoint.TypeName:
                WriteUri(bufferWriter, endPoint);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(endPoint));
        }

        static void WriteIp(TWriter bufferWriter, IPEndPoint ip)
        {
            // the format is:
            // IP endpoint type = 1 byte
            // port = 4 bytes
            // number of address bytes, N = 1 byte
            // address bytes = N bytes
            var writer = new SpanWriter<byte>(bufferWriter.GetSpan(prefixSize + sizeof(int)));
            writer += IPEndPointPrefix;
            writer.WriteLittleEndian(ip.Port);
            bufferWriter.Advance(writer.WrittenCount);

            SerializeAddress(bufferWriter, ip.Address);
        }

        static void WriteHttp(TWriter bufferWriter, HttpEndPoint endPoint)
        {
            // the format is:
            // DNS endpoint type = 1 byte
            // HTTPS (true/false) = 1 byte
            // port = 4 bytes
            // address family = 4 bytes
            // host name length, N = 4 bytes
            // host name = N bytes
            var writer = new SpanWriter<byte>(bufferWriter.GetSpan(prefixSize + sizeof(byte) + sizeof(int) + sizeof(AddressFamily)));
            writer += HttpEndPointPrefix;
            writer += Unsafe.BitCast<bool, byte>(endPoint.IsSecure);
            writer.WriteLittleEndian(endPoint.Port);
            writer.WriteLittleEndian<Enum<AddressFamily>>(new(endPoint.AddressFamily));
            bufferWriter.Advance(writer.WrittenCount);

            SerializeHost(bufferWriter, endPoint.Host);
        }

        static void WriteDns(TWriter bufferWriter, DnsEndPoint endPoint)
        {
            // the format is:
            // DNS endpoint type = 1 byte
            // port = 4 bytes
            // address family = 4 bytes
            // host name length, N = 4 bytes
            // host name = N bytes
            var writer = new SpanWriter<byte>(bufferWriter.GetSpan(prefixSize + sizeof(int) + sizeof(AddressFamily)));
            writer += DnsEndPointPrefix;
            writer.WriteLittleEndian(endPoint.Port);
            writer.WriteLittleEndian<Enum<AddressFamily>>(new(endPoint.AddressFamily));
            bufferWriter.Advance(writer.WrittenCount);

            SerializeHost(bufferWriter, endPoint.Host);
        }

        static void WriteUds(TWriter bufferWriter, UnixDomainSocketEndPoint endPoint)
        {
            // the format is:
            // UDS endpoint type = 1 byte
            // path name length, N = 4 bytes
            // path name = N bytes
            bufferWriter.GetSpan()[0] = DomainSocketEndPointPrefix;
            bufferWriter.Advance(prefixSize);

            SerializeHost(bufferWriter, endPoint.ToString());
        }

        static void WriteUri(TWriter bufferWriter, EndPoint endPoint)
        {
            // the format is:
            // URI endpoint type = 1 byte
            // URI length, N = 4 bytes
            // URI = N bytes
            bufferWriter.GetSpan()[0] = UriEndPointPrefix;
            bufferWriter.Advance(prefixSize);

            SerializeHost(bufferWriter, UriEndPoint.GetUri(endPoint).ToString());
        }

        static void SerializeAddress(TWriter bufferWriter, IPAddress address)
        {
            var buffer = bufferWriter.GetSpan(IPv6AddressSize + 1);

            if (!address.TryWriteBytes(buffer[1..], out var bytesWritten))
                throw new NotSupportedException();

            buffer[0] = (byte)bytesWritten;
            bufferWriter.Advance(bytesWritten + 1);
        }

        static void SerializeHost(TWriter bufferWriter, ReadOnlySpan<char> hostName)
        {
            var count = HostNameEncoding.GetByteCount(hostName);
            var writer = new SpanWriter<byte>(bufferWriter.GetSpan(sizeof(int) + count));
            writer.WriteLittleEndian(count);
            writer.Advance(HostNameEncoding.GetBytes(hostName, writer.RemainingSpan));

            bufferWriter.Advance(writer.WrittenCount);
        }
    }

    /// <summary>
    /// Deserializes endpoint address.
    /// </summary>
    /// <param name="reader">The binary reader.</param>
    /// <returns>The deserialized network endpoint address.</returns>
    public static EndPoint ReadEndPoint(this ref SequenceReader reader)
    {
        return reader.ReadByte() switch
        {
            IPEndPointPrefix => ReadIp(ref reader),
            DnsEndPointPrefix => ReadDns(ref reader),
            HttpEndPointPrefix => ReadHttp(ref reader),
            DomainSocketEndPointPrefix => ReadDomainSocket(ref reader),
            UriEndPointPrefix => ReadUri(ref reader),
            _ => throw new NotSupportedException(),
        };
        
        static UnixDomainSocketEndPoint ReadDomainSocket(ref SequenceReader reader)
        {
            var length = reader.ReadLittleEndian<int>();

            using var pathBuffer = (uint)length <= (uint)SpanOwner<byte>.StackallocThreshold
                ? stackalloc byte[length]
                : new SpanOwner<byte>(length);

            reader.Read(pathBuffer.Span);
            return new(HostNameEncoding.GetString(pathBuffer.Span));
        }
        
        static EndPoint ReadUri(ref SequenceReader reader)
        {
            var length = reader.ReadLittleEndian<int>();

            using var pathBuffer = (uint)length <= (uint)SpanOwner<byte>.StackallocThreshold
                ? stackalloc byte[length]
                : new SpanOwner<byte>(length);

            reader.Read(pathBuffer.Span);
            return UriEndPoint.Create(new Uri(HostNameEncoding.GetString(pathBuffer.Span), UriKind.Absolute));
        }
        
        [SkipLocalsInit]
        static IPEndPoint ReadIp(ref SequenceReader reader)
        {
            var port = reader.ReadLittleEndian<int>();
            var bytesCount = reader.ReadByte();

            Span<byte> bytes = stackalloc byte[bytesCount];
            reader.Read(bytes);

            return new(new IPAddress(bytes), port);
        }

        static DnsEndPoint ReadDns(ref SequenceReader reader)
            => new(reader.DeserializeHost(out var port, out var family), port, family);

        static HttpEndPoint ReadHttp(ref SequenceReader reader)
        {
            var secure = Unsafe.BitCast<byte, bool>(reader.ReadByte());
            return new(reader.DeserializeHost(out var port, out var family), port, secure, family);
        }
    }

    private static string DeserializeHost(this ref SequenceReader reader, out int port, out AddressFamily family)
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