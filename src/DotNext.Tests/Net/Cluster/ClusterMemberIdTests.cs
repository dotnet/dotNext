using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace DotNext.Net.Cluster;

using Buffers;

[ExcludeFromCodeCoverage]
public sealed class ClusterMemberIdTests : Test
{
    [Fact]
    public static void Equality()
    {
        var id1 = new ClusterMemberId(Random.Shared);
        var id2 = new ClusterMemberId(Random.Shared);
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
    public static void RestoreFromBytes()
    {
        var id1 = new ClusterMemberId(Random.Shared);
        Span<byte> bytes = stackalloc byte[ClusterMemberId.Size];
        var writer = new SpanWriter<byte>(bytes);
        id1.Format(ref writer);

        var id2 = new ClusterMemberId(bytes);
        Equal(id1, id2);
    }

    [Fact]
    public static void Parsing()
    {
        var expected = new ClusterMemberId(Random.Shared);
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