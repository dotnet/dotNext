using System;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace DotNext.Net.Http
{
    internal static class HttpUtils
    {
        // TODO: Replace with HttpVersion.Http3 in .NET 6
        private static readonly Version Http3 = new(3, 0);

        internal static void SetProtocolVersion(this HttpClient client, HttpProtocolVersion version)
        {
            switch (version)
            {
                case HttpProtocolVersion.Http1:
                    client.DefaultRequestVersion = HttpVersion.Version11;
                    break;
                case HttpProtocolVersion.Http2:
                    client.DefaultRequestVersion = HttpVersion.Version20;
                    break;
                case HttpProtocolVersion.Http3:
                    client.DefaultRequestVersion = Http3;
                    break;
            }
        }

        internal static void SetProtocolVersion(this ListenOptions options, HttpProtocolVersion version, HttpVersionPolicy policy)
        {
            switch (policy)
            {
                case HttpVersionPolicy.RequestVersionExact:
                    SetExactVersion(options, version);
                    break;
                case HttpVersionPolicy.RequestVersionOrHigher:
                    SetVersionOrHigher(options, version);
                    break;
                case HttpVersionPolicy.RequestVersionOrLower:
                default:
                    SetVersionOrLower(options, version);
                    break;
            }


            static void SetExactVersion(ListenOptions options, HttpProtocolVersion version)
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

            static void SetVersionOrLower(ListenOptions options, HttpProtocolVersion version)
            {
                switch (version)
                {
                    case HttpProtocolVersion.Http1:
                        options.Protocols = HttpProtocols.Http1;
                        break;
                    case HttpProtocolVersion.Http2:
                        options.Protocols = HttpProtocols.Http1AndHttp2;
                        break;
                    case HttpProtocolVersion.Http3:
#if NETCOREAPP3_1
                        options.Protocols = (HttpProtocols)7;
#else
                        options.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
#endif
                        break;
                }
            }

            static void SetVersionOrHigher(ListenOptions options, HttpProtocolVersion version)
            {
                switch (version)
                {
                    case HttpProtocolVersion.Http1:
                        options.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                        break;
                    case HttpProtocolVersion.Http2:
                        options.Protocols = HttpProtocols.Http2 | HttpProtocols.Http3;
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
}