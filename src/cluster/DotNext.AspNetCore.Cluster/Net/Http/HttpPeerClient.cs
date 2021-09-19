using System.Net;

namespace DotNext.Net.Http;

/// <summary>
/// Represents HTTP client that can be used to communicate with the peer.
/// </summary>
public class HttpPeerClient : HttpClient, IPeer
{
    internal HttpPeerClient(Uri address, HttpMessageHandler handler, bool disposeHandler)
        : base(handler, disposeHandler)
    {
        BaseAddress = address;
    }

    /// <inheritdoc />
    EndPoint IPeer.EndPoint => new HttpEndPoint(BaseAddress!);
}