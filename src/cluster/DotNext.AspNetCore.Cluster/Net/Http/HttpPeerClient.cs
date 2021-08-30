using System;
using System.Net;
using System.Net.Http;

namespace DotNext.Net.Http
{
    /// <summary>
    /// Represents HTTP client that can be used to communicate with the peer.
    /// </summary>
    public sealed class HttpPeerClient : HttpClient, IPeer
    {
        internal HttpPeerClient(Uri address, HttpMessageHandler handler, bool disposeHandler)
            : base(handler, disposeHandler)
        {
            BaseAddress = address;
        }

        /// <inheritdoc />
        EndPoint IPeer.EndPoint => new HttpEndPoint(BaseAddress!);
    }
}