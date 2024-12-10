using System.Net.Sockets;
using Microsoft.AspNetCore.Components.Forms;

namespace DotNext.Net.Http;

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

    [Fact]
    public static void Format()
    {
        const string expected = "http://localhost:3262/";
        Span<char> buffer = stackalloc char[64];
        ISpanFormattable formattable = new HttpEndPoint(new Uri(expected));
        True(formattable.TryFormat(buffer, out var charsWritten, ReadOnlySpan<char>.Empty, provider: null));
        
        Equal(expected, buffer.Slice(0, charsWritten));
        Equal(expected, formattable.ToString(format: null, formatProvider: null));
    }

    [Fact]
    public static void Parse()
    {
        const string expected = "http://localhost:3262/";
        Equal(expected, Parse<HttpEndPoint>(expected).ToString());
        True(TryParse<HttpEndPoint>(expected, out var ep));
        Equal(expected, ep.ToString());
        
        static T Parse<T>(string input)
            where T : IParsable<T>
            => T.Parse(input, provider: null);

        static bool TryParse<T>(string input, out T result)
            where T : IParsable<T>
            => T.TryParse(input, provider: null, out result);
    }
}