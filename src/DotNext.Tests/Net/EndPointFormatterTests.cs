using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net;

using Buffers;
using IO;
using HttpEndPoint = Http.HttpEndPoint;

public sealed class EndPointFormatterTests : Test
{
    public static IEnumerable<object[]> GetTestEndPoints()
    {
        yield return new object[] { new DnsEndPoint("host", 3262), EqualityComparer<EndPoint>.Default };
        yield return new object[] { new IPEndPoint(IPAddress.Parse("192.168.0.1"), 3263), EqualityComparer<EndPoint>.Default };
        yield return new object[] { new IPEndPoint(IPAddress.Parse("2001:0db8:0000:0000:0000:8a2e:0370:7334"), 3264), EqualityComparer<EndPoint>.Default };
        yield return new object[] { new HttpEndPoint("192.168.0.1", 3262, true, AddressFamily.InterNetwork), EqualityComparer<EndPoint>.Default };
        yield return new object[] { new HttpEndPoint("192.168.0.1", 3262, false, AddressFamily.InterNetwork), EqualityComparer<EndPoint>.Default };
        yield return new object[] { new HttpEndPoint("2001:0db8:0000:0000:0000:8a2e:0370:7334", 3262, true, AddressFamily.InterNetworkV6), EqualityComparer<EndPoint>.Default };
        yield return new object[] { new HttpEndPoint("host", 3262, true), EqualityComparer<EndPoint>.Default };
        yield return new object[] { new HttpEndPoint("host", 3262, false), EqualityComparer<EndPoint>.Default };
        yield return new object[] { new UriEndPoint(new Uri("http://host:3262/")), EndPointFormatter.UriEndPointComparer };
        yield return new object[] { new UriEndPoint(new Uri("http://host/path/to/resource")), EndPointFormatter.UriEndPointComparer };

        if (Socket.OSSupportsUnixDomainSockets)
        {
            yield return new object[] { new UnixDomainSocketEndPoint("@abstract"), EqualityComparer<EndPoint>.Default };
            yield return new object[] { new UnixDomainSocketEndPoint(Path.GetTempFileName()), EqualityComparer<EndPoint>.Default };
        }
    }

    [Theory]
    [MemberData(nameof(GetTestEndPoints))]
    public static void SerializeDeserializeEndPoint(EndPoint expected, IEqualityComparer<EndPoint> comparer)
    {
        byte[] data;
        var writer = new BufferWriterSlim<byte>(32);
        try
        {
            writer.WriteEndPoint(expected);
            data = writer.WrittenSpan.ToArray();
        }
        finally
        {
            writer.Dispose();
        }

        var reader = IAsyncBinaryReader.Create(data);
        Equal(expected, reader.ReadEndPoint(), comparer);
    }

    [Theory]
    [MemberData(nameof(GetTestEndPoints))]
    public static void BufferizeDeserializeEndPoint(EndPoint expected, IEqualityComparer<EndPoint> comparer)
    {
        using var buffer = expected.GetBytes();

        var reader = IAsyncBinaryReader.Create(buffer.Memory);
        Equal(expected, reader.ReadEndPoint(), comparer);
    }
}