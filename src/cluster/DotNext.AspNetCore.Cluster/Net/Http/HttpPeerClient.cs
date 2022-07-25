using System.Net;
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Http;

/// <summary>
/// Represents HTTP client that can be used to communicate with the peer.
/// </summary>
public class HttpPeerClient : HttpClient, IPeer
{
    internal HttpPeerClient(Uri address, HttpMessageHandler handler, bool disposeHandler)
        : base(handler, disposeHandler)
        => BaseAddress = address;

    /// <inheritdoc />
    EndPoint IPeer.EndPoint => new UriEndPoint(BaseAddress!);
}