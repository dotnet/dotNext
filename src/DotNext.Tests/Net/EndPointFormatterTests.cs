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
        // TODO: Remove in .NET 7/8 because of https://github.com/dotnet/runtime/pull/69722
        private sealed class UnixDomainSocketEndPointComparer : IEqualityComparer<EndPoint>
        {
            internal static readonly UnixDomainSocketEndPointComparer Instance = new();

            bool IEqualityComparer<EndPoint>.Equals(EndPoint x, EndPoint y)
                => string.Equals(x?.ToString(), y?.ToString(), StringComparison.Ordinal);

            int IEqualityComparer<EndPoint>.GetHashCode(EndPoint obj)
                => string.GetHashCode(obj.ToString(), StringComparison.Ordinal);
        }

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

            if (Socket.OSSupportsUnixDomainSockets)
            {
                yield return new object[] { new UnixDomainSocketEndPoint("@abstract"), UnixDomainSocketEndPointComparer.Instance };
                yield return new object[] { new UnixDomainSocketEndPoint(Path.GetTempFileName()), UnixDomainSocketEndPointComparer.Instance };
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
}