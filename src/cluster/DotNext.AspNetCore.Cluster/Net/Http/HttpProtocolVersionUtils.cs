using System;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DotNext.Net.Http
{
    internal static class HttpProtocolVersionUtils
    {
        // TODO: Replace with HttpVersion.Http3 in .NET 6
        private static readonly Version Http3 = new(3, 0);

        internal static void SetMaxProtocolVersion(this HttpRequestMessage request, HttpProtocolVersion version)
        {
            switch (version)
            {
                case HttpProtocolVersion.Http1:
                    request.Version = HttpVersion.Version11;
                    break;
                case HttpProtocolVersion.Http2:
                    request.Version = HttpVersion.Version20;
                    break;
                case HttpProtocolVersion.Http3:
                    request.Version = Http3;
                    break;
            }
        }

        internal static void SetMaxProtocolVersion(this ListenOptions options, HttpProtocolVersion version)
        {
            switch (version)
            {
                case HttpProtocolVersion.Http1:
                    options.Protocols = HttpProtocols.Http1;
                    break;
                case HttpProtocolVersion.Http2:
                    options.Protocols = HttpProtocols.Http2;
                    break;
                case HttpProtocolVersion.Http3:
#if NETCOREAPP3_1
                    options.Protocols = (HttpProtocols)4;
#else
                    options.Protocols = HttpProtocols.Http3;
#endif
                    break;
            }
        }
    }
}