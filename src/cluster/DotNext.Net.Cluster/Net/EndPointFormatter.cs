using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DotNext.Net;

using Buffers;
using IO;
using HttpEndPoint = Net.Http.HttpEndPoint;
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

            if (!writer.TryDetachBuffer(out result))
                result = writer.WrittenSpan.Copy(allocator);
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
            case HttpEndPoint http:
                // the format is:
                // DNS endpoint type = 1 byte
                // HTTPS (true/false) = 1 byte
                // port = 4 bytes
                // address family = 4 bytes
                // host name length, N = 4 bytes
                // host name = N bytes
                writer.Add(HttpEndPointPrefix);
                writer.Add(http.IsSecure.ToByte());
                writer.WriteInt32(http.Port, true);
                writer.WriteInt32((int)http.AddressFamily, true);
                Serialize(http.Host, ref writer);
                break;
            case DnsEndPoint dns:
                // the format is:
                // DNS endpoint type = 1 byte
                // port = 4 bytes
                // address family = 4 bytes
                // host name length, N = 4 bytes
                // host name = N bytes
                writer.Add(DnsEndPointPrefix);
                writer.WriteInt32(dns.Port, true);
                writer.WriteInt32((int)dns.AddressFamily, true);
                Serialize(dns.Host, ref writer);
                break;
            case UnixDomainSocketEndPoint domainSocket:
                // the format is:
                // UDS endpoint type = 1 byte
                // path name length, N = 4 bytes
                // path name = N bytes
                writer.Add(DomainSocketEndPointPrefix);
                Serialize(domainSocket.ToString(), ref writer);
                break;
            case UriEndPoint uri:
                // the format is:
                // URI endpoint type = 1 byte
                // URI length, N = 4 bytes
                // URI = N bytes
                writer.Add(UriEndPointPrefix);
                Serialize(uri.ToString(), ref writer);
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
    /// Deserializes endpoint address.
    /// </summary>
    /// <param name="reader">The binary reader.</param>
    /// <returns>The deserialized network endpoint address.</returns>
    public static EndPoint ReadEndPoint(this ref SequenceReader reader) => reader.Read<byte>() switch
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
        var length = reader.ReadInt32(true);

        using var pathBuffer = (uint)length <= (uint)MemoryRental<byte>.StackallocThreshold ? stackalloc byte[length] : new MemoryRental<byte>(length, true);
        reader.Read(pathBuffer.Span);
        return new(HostNameEncoding.GetString(pathBuffer.Span));
    }

    private static UriEndPoint DeserializeUri(ref SequenceReader reader)
    {
        var length = reader.ReadInt32(true);

        using var pathBuffer = (uint)length <= (uint)MemoryRental<byte>.StackallocThreshold ? stackalloc byte[length] : new MemoryRental<byte>(length, true);
        reader.Read(pathBuffer.Span);
        return new(new Uri(HostNameEncoding.GetString(pathBuffer.Span), UriKind.Absolute));
    }

    private static IPEndPoint DeserializeIP(ref SequenceReader reader)
    {
        var port = reader.ReadInt32(true);
        var bytesCount = reader.Read<byte>();

        Span<byte> bytes = stackalloc byte[bytesCount];
        reader.Read(bytes);

        return new(new IPAddress(bytes), port);
    }

    private static void DeserializeHost(ref SequenceReader reader, out string hostName, out int port, out AddressFamily family)
    {
        port = reader.ReadInt32(true);
        family = (AddressFamily)reader.ReadInt32(true);
        var length = reader.ReadInt32(true);

        using var hostNameBuffer = (uint)length <= (uint)MemoryRental<byte>.StackallocThreshold ? stackalloc byte[length] : new MemoryRental<byte>(length, true);
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
        var secure = BasicExtensions.ToBoolean(reader.Read<byte>());
        DeserializeHost(ref reader, out var hostName, out var port, out var family);
        return new(hostName, port, secure, family);
    }
}