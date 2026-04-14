using System.Buffers;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotNext.Net;

using Buffers;
using Numerics;
using HttpEndPoint = Http.HttpEndPoint;
using UriEndPoint = Microsoft.AspNetCore.Connections.UriEndPoint;

/// <summary>
/// Provides methods for serialization/deserialization of <see cref="EndPoint"/> derived types.
/// </summary>
/// <remarks>
/// List of supported endpoint types: <see cref="IPEndPoint"/>, <see cref="DnsEndPoint"/>,
/// <see cref="HttpEndPoint"/>, <see cref="UnixDomainSocketEndPoint"/>, <see cref="UriEndPoint"/>.
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
    /// Gets comparer for <see cref="UriEndPoint"/>.
    /// </summary>
    [CLSCompliant(false)]
    public static IEqualityComparer<EndPoint> UriEndPointComparer { get; } = new UriEndPointComparer();

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
            case IPEndPoint ip:
                WriteIP(bufferWriter, ip);
                break;
            case HttpEndPoint http:
                WriteHttp(bufferWriter, http);
                break;
            case DnsEndPoint dns:
                WriteDns(bufferWriter, dns);
                break;
            case UnixDomainSocketEndPoint domainSocket:
                WriteUds(bufferWriter, domainSocket);
                break;
            case UriEndPoint uri:
                WriteUri(bufferWriter, uri);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(endPoint));
        }

        static void WriteIP(TWriter bufferWriter, IPEndPoint ip)
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
            
            Serialize(bufferWriter, ip.Address);
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
            
            Serialize(bufferWriter, endPoint.Host);
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

            Serialize(bufferWriter, endPoint.Host);
        }

        static void WriteUds(TWriter bufferWriter, UnixDomainSocketEndPoint endPoint)
        {
            // the format is:
            // UDS endpoint type = 1 byte
            // path name length, N = 4 bytes
            // path name = N bytes
            bufferWriter.GetSpan()[0] = DomainSocketEndPointPrefix;
            bufferWriter.Advance(prefixSize);

            Serialize(bufferWriter, endPoint.ToString());
        }

        static void WriteUri(TWriter bufferWriter, UriEndPoint endPoint)
        {
            // the format is:
            // URI endpoint type = 1 byte
            // URI length, N = 4 bytes
            // URI = N bytes
            bufferWriter.GetSpan()[0] = UriEndPointPrefix;
            bufferWriter.Advance(prefixSize);

            Serialize(bufferWriter, endPoint.Uri.ToString());
        }
    }

    private static void Serialize<TWriter>(TWriter bufferWriter, IPAddress address)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var buffer = bufferWriter.GetSpan(IPv6AddressSize + 1);

        if (!address.TryWriteBytes(buffer[1..], out var bytesWritten))
            throw new NotSupportedException();

        buffer[0] = (byte)bytesWritten;
        bufferWriter.Advance(bytesWritten + 1);
    }

    private static void Serialize<TWriter>(TWriter bufferWriter, ReadOnlySpan<char> hostName)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var count = HostNameEncoding.GetByteCount(hostName);
        var writer = new SpanWriter<byte>(bufferWriter.GetSpan(sizeof(int) + count));
        writer.WriteLittleEndian(count);
        writer.Advance(HostNameEncoding.GetBytes(hostName, writer.RemainingSpan));

        bufferWriter.Advance(writer.WrittenCount);
    }

    /// <summary>
    /// Deserializes endpoint address.
    /// </summary>
    /// <param name="reader">The binary reader.</param>
    /// <returns>The deserialized network endpoint address.</returns>
    public static EndPoint ReadEndPoint(this ref SequenceReader reader) => reader.ReadByte() switch
    {
        IPEndPointPrefix => DeserializeIP(ref reader),
        DnsEndPointPrefix => DeserializeHost(ref reader),
        HttpEndPointPrefix => DeserializeHttp(ref reader),
        DomainSocketEndPointPrefix => DeserializeDomainSocket(ref reader),
        UriEndPointPrefix => DeserializeUri(ref reader),
        _ => throw new NotSupportedException(),
    };

    private static UnixDomainSocketEndPoint DeserializeDomainSocket(ref SequenceReader reader)
    {
        var length = reader.ReadLittleEndian<int>();

        using var pathBuffer = (uint)length <= (uint)SpanOwner<byte>.StackallocThreshold
            ? stackalloc byte[length]
            : new SpanOwner<byte>(length);

        reader.Read(pathBuffer.Span);
        return new(HostNameEncoding.GetString(pathBuffer.Span));
    }

    private static UriEndPoint DeserializeUri(ref SequenceReader reader)
    {
        var length = reader.ReadLittleEndian<int>();

        using var pathBuffer = (uint)length <= (uint)SpanOwner<byte>.StackallocThreshold
            ? stackalloc byte[length]
            : new SpanOwner<byte>(length);

        reader.Read(pathBuffer.Span);
        return new(new Uri(HostNameEncoding.GetString(pathBuffer.Span), UriKind.Absolute));
    }

    private static IPEndPoint DeserializeIP(ref SequenceReader reader)
    {
        var port = reader.ReadLittleEndian<int>();
        var bytesCount = reader.ReadByte();

        Span<byte> bytes = stackalloc byte[bytesCount];
        reader.Read(bytes);

        return new(new IPAddress(bytes), port);
    }

    private static void DeserializeHost(ref SequenceReader reader, out string hostName, out int port, out AddressFamily family)
    {
        port = reader.ReadLittleEndian<int>();
        family = (AddressFamily)reader.ReadLittleEndian<int>();
        var length = reader.ReadLittleEndian<int>();

        using var hostNameBuffer = (uint)length <= (uint)SpanOwner<byte>.StackallocThreshold
            ? stackalloc byte[length]
            : new SpanOwner<byte>(length);
        reader.Read(hostNameBuffer.Span);
        hostName = HostNameEncoding.GetString(hostNameBuffer.Span);
    }

    private static DnsEndPoint DeserializeHost(ref SequenceReader reader)
    {
        DeserializeHost(ref reader, out var hostName, out var port, out var family);
        return new(hostName, port, family);
    }

    private static HttpEndPoint DeserializeHttp(ref SequenceReader reader)
    {
        var secure = Unsafe.BitCast<byte, bool>(reader.ReadByte());
        DeserializeHost(ref reader, out var hostName, out var port, out var family);
        return new(hostName, port, secure, family);
    }
}