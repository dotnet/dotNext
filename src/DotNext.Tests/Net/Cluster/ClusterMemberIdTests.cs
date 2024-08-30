using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Cluster;

using Buffers;

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
        id1.Format(bytes);

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

    public static TheoryData<EndPoint> GetEndPoints() => new()
    {
        new IPEndPoint(IPAddress.Parse("127.0.0.1"), 3262),
        new DnsEndPoint("localhost", 3262),
        new UnixDomainSocketEndPoint("/path"),
        new UriEndPoint(new Uri("https://localhost:3232/path", UriKind.Absolute)),
        new UriEndPoint(new Uri("http://localhost", UriKind.Absolute)),
    };

    [Theory]
    [MemberData(nameof(GetEndPoints))]
    public static void CreateFromEndPoint(EndPoint endpoint)
    {
        var id = ClusterMemberId.FromEndPoint(endpoint).ToString();
        True(ClusterMemberId.TryParse(id, out var actual));
        Equal(ClusterMemberId.FromEndPoint(endpoint), actual);
    }
}