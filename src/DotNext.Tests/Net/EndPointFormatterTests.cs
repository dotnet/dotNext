using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace DotNext.Net
{
    using Buffers;
    using IO;
    using HttpEndPoint = Http.HttpEndPoint;

    [ExcludeFromCodeCoverage]
    public sealed class EndPointFormatterTests : Test
    {
        public static IEnumerable<object[]> GetTestEndPoints()
        {
            yield return new object[] { new DnsEndPoint("host", 3262) };
            yield return new object[] { new IPEndPoint(IPAddress.Parse("192.168.0.1"), 3263) };
            yield return new object[] { new IPEndPoint(IPAddress.Parse("2001:0db8:0000:0000:0000:8a2e:0370:7334"), 3264) };
            yield return new object[] { new HttpEndPoint("192.168.0.1", 3262, true, AddressFamily.InterNetwork) };
            yield return new object[] { new HttpEndPoint("192.168.0.1", 3262, false, AddressFamily.InterNetwork) };
            yield return new object[] { new HttpEndPoint("2001:0db8:0000:0000:0000:8a2e:0370:7334", 3262, true, AddressFamily.InterNetworkV6) };
            yield return new object[] { new HttpEndPoint("host", 3262, true) };
            yield return new object[] { new HttpEndPoint("host", 3262, false) };

            // if (Socket.OSSupportsUnixDomainSockets)
            // {
            //     yield return new object[] { new UnixDomainSocketEndPoint("@abstract") };
            //     yield return new object[] { new UnixDomainSocketEndPoint(Path.GetTempFileName()) };
            // }
        }

        [Theory]
        [MemberData(nameof(GetTestEndPoints))]
        public static void SerializeDeserializeEndPoint(EndPoint expected)
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
            Equal(expected, reader.ReadEndPoint());
        }

        [Theory]
        [MemberData(nameof(GetTestEndPoints))]
        public static void BufferizeDeserializeEndPoint(EndPoint expected)
        {
            using var buffer = expected.GetBytes();

            var reader = IAsyncBinaryReader.Create(buffer.Memory);
            Equal(expected, reader.ReadEndPoint());
        }
    }
}