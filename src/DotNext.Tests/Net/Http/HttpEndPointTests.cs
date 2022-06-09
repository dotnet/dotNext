using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace DotNext.Net.Http;

[ExcludeFromCodeCoverage]
public sealed class HttpEndPointTests : Test
{
    [Fact]
    public static void EqualityOperators()
    {
        var ep1 = new HttpEndPoint("host", 22, true);
        var ep2 = new HttpEndPoint("host", 22, true);
        Equal(ep1, ep2);
        Equal(ep1.GetHashCode(), ep2.GetHashCode());
        True(ep1 == ep2);
        False(ep1 != ep2);

        ep2 = new HttpEndPoint("host", 22, false);
        NotEqual(ep1, ep2);
        NotEqual(ep1.GetHashCode(), ep2.GetHashCode());
        False(ep1 == ep2);
        True(ep1 != ep2);

        ep2 = new HttpEndPoint("host", 22, true, AddressFamily.InterNetworkV6);
        NotEqual(ep1, ep2);
        NotEqual(ep1.GetHashCode(), ep2.GetHashCode());
        False(ep1 == ep2);
        True(ep1 != ep2);
    }

    [Fact]
    public static void ParseEndPoint()
    {
        var ep = new HttpEndPoint(new Uri("HtTps://localhost:3232", UriKind.Absolute));
        True(ep.IsSecure);
        Equal(Uri.UriSchemeHttps, ep.Scheme);
        Equal(3232, ep.Port);
        Equal(AddressFamily.Unspecified, ep.AddressFamily);
        Equal("localhost", ep.Host, ignoreCase: true);
        Equal("https://localhost:3232/", ep.ToString());

        ep = new HttpEndPoint(new Uri("HTTP://192.168.0.1", UriKind.Absolute));
        False(ep.IsSecure);
        Equal(Uri.UriSchemeHttp, ep.Scheme);
        Equal(80, ep.Port);
        Equal(AddressFamily.InterNetwork, ep.AddressFamily);
        Equal("192.168.0.1", ep.Host, ignoreCase: true);
        Equal("http://192.168.0.1:80/", ep.ToString());

        True(HttpEndPoint.TryParse("https://localhost/", out ep));
        Equal(443, ep.Port);
        True(ep.IsSecure);
        Equal("localhost", ep.Host, ignoreCase: true);
        Equal("https://localhost:443/", ep.ToString());

        False(HttpEndPoint.TryParse("wrong-string", out ep));
        Null(ep);
    }

    [Fact]
    public static void ToUri()
    {
        var ep = new HttpEndPoint("localhost", 3262, true);
        var uri = ep.CreateUriBuilder().Uri;
        Equal(Uri.UriSchemeHttps, uri.Scheme, ignoreCase: true);
        Equal(3262, ep.Port);
    }
}