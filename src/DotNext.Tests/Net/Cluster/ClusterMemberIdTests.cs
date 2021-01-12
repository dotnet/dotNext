using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Xunit;

namespace DotNext.Net.Cluster
{
    [ExcludeFromCodeCoverage]
    public sealed class ClusterMemberIdTests : Test
    {
        [Fact]
        public static void Equality()
        {
            var random = new Random();
            var id1 = random.Next<ClusterMemberId>();
            var id2 = random.Next<ClusterMemberId>();
            NotEqual(id1, id2);
            False(id1 == id2);
            True(id1 != id2);
            False(id1.Equals(id2));
            id2 = id1;
            Equal(id1, id2);
            True(id1 == id2);
            False(id1 != id2);
            True(id1.Equals(id2));
        }

        [Fact]
        public static void SerializationDeserialization()
        {
            var id = new Random().Next<ClusterMemberId>();
            Equal(id, SerializeDeserialize(id));
        }

        [Fact]
        public static unsafe void RestoreFromBytes()
        {
            var id1 = new Random().Next<ClusterMemberId>();
            var bytes = Span.AsReadOnlyBytes(id1);
            var id2 = new ClusterMemberId(bytes);
            Equal(id1, id2);
        }

        [Fact]
        public static void Parsing()
        {
            var expected = new Random().Next<ClusterMemberId>();
            True(ClusterMemberId.TryParse(expected.ToString().AsSpan(), out var actual));
            Equal(expected, actual);
            var invalidHex = "AB142244";
            False(ClusterMemberId.TryParse(invalidHex.AsSpan(), out _));
        }

        [Fact]
        public static void FromIP()
        {
            var endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 3262);
            var id = ClusterMemberId.FromEndPoint(endpoint).ToString();
            True(ClusterMemberId.TryParse(id, out var actual));
            Equal(ClusterMemberId.FromEndPoint(endpoint), actual);
        }

        [Fact]
        public static void FromDNS()
        {
            var endpoint = new DnsEndPoint("localhost", 3262);
            var id = ClusterMemberId.FromEndPoint(endpoint).ToString();
            True(ClusterMemberId.TryParse(id, out var actual));
            Equal(ClusterMemberId.FromEndPoint(endpoint), actual);
        }

        [Fact]
        public static void FromUDS()
        {
            var endpoint = new UnixDomainSocketEndPoint("/path");
            var id = ClusterMemberId.FromEndPoint(endpoint).ToString();
            True(ClusterMemberId.TryParse(id, out var actual));
            Equal(ClusterMemberId.FromEndPoint(endpoint), actual);
        }
    }
}