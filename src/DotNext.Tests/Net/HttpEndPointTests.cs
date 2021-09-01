using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using Xunit;

namespace DotNext.Net
{
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

            ep = new HttpEndPoint(new Uri("HTTP://192.168.0.1", UriKind.Absolute));
            False(ep.IsSecure);
            Equal(Uri.UriSchemeHttp, ep.Scheme);
            Equal(80, ep.Port);
            Equal(AddressFamily.InterNetwork, ep.AddressFamily);
            Equal("192.168.0.1", ep.Host, ignoreCase: true);

            True(HttpEndPoint.TryParse("https://localhost/", out ep));
            Equal(443, ep.Port);
            True(ep.IsSecure);
            Equal("localhost", ep.Host, ignoreCase: true);

            False(HttpEndPoint.TryParse("wrong-string", out ep));
            Null(ep);
        }
    }
}