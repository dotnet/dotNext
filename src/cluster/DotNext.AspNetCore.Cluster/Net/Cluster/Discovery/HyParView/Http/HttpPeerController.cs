using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Discovery.HyParView.Http
{
    using Buffers;
    using Net.Http;
    using TransportLayerSecurityFeature = Hosting.Server.Features.TransportLayerSecurityFeature;

    [SuppressMessage("Performance", "CA1812", Justification = "This class is instantiated by DI container")]
    internal sealed partial class HttpPeerController : PeerController, IHostedService, IPeerMesh<HttpPeerClient>
    {
        private const string UserAgent = "HyParView.NET";
        private readonly IHttpMessageHandlerFactory? handlerFactory;
        private readonly IPeerLifetime? lifetimeService;
        private readonly MemoryAllocator<byte>? allocator;
        private readonly string clientHandlerName;
        private readonly TimeSpan connectTimeout, requestTimeout;
        private readonly HttpProtocolVersion protocolVersion;
#if !NETCOREAPP3_1
        private readonly HttpVersionPolicy protocolVersionPolicy;
#endif
        private readonly ConcurrentDictionary<EndPoint, HttpPeerClient> clientCache;
        private readonly Uri resourcePath;
        private readonly IServer server;

        private EndPoint? localNode, contactNode;

        // TODO: Use nullable services in .NET 6
        private HttpPeerController(IOptions<HttpPeerConfiguration> configuration, IServiceProvider dependencies)
            : base(configuration.Value)
        {
            // configuration
            clientHandlerName = configuration.Value.ClientHandlerName;
            connectTimeout = configuration.Value.ConnectTimeout;
            requestTimeout = configuration.Value.RequestTimeout;
            allocator = configuration.Value.Allocator;
            protocolVersion = configuration.Value.ProtocolVersion;
#if !NETCOREAPP3_1
            protocolVersionPolicy = configuration.Value.ProtocolVersionPolicy;
#endif
            contactNode = configuration.Value.ContactNode;
            localNode = configuration.Value.LocalNode;
            resourcePath = new(configuration.Value.ResourcePath.Value.IfNullOrEmpty(HttpPeerConfiguration.DefaultResourcePath), UriKind.Relative);

            // resolve dependencies
            handlerFactory = dependencies.GetService<IHttpMessageHandlerFactory>();
            lifetimeService = dependencies.GetService<IPeerLifetime>();
            Logger = dependencies.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
            server = dependencies.GetRequiredService<IServer>();

            // various init
            clientCache = new();
        }

        internal HttpPeerController(IServiceProvider dependencies)
            : this(dependencies.GetRequiredService<IOptions<HttpPeerConfiguration>>(), dependencies)
        {
        }

        /// <summary>
        /// Gets the logger associated with this controller.
        /// </summary>
        protected sealed override ILogger Logger { get; }

        internal PathString ResourcePath => PathString.FromUriComponent(resourcePath);

        private bool IsTlsEnabled => server.Features.Get<TransportLayerSecurityFeature>()?.IsEnabled ?? true;

        private Uri CreateBaseUri(EndPoint peer)
        {
            var builder = new UriBuilder() { Scheme = Uri.UriSchemeHttps };
            builder.Scheme = protocolVersion switch
            {
                HttpProtocolVersion.Http1 or HttpProtocolVersion.Auto => IsTlsEnabled ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
                _ => Uri.UriSchemeHttps,
            };

            switch (peer)
            {
                case IPEndPoint ip:
                    builder.Host = ip.Address.ToString();
                    builder.Port = ip.Port;
                    break;
                case DnsEndPoint dns:
                    builder.Host = dns.Host;
                    builder.Port = dns.Port;
                    break;
                default:
                    throw new NotSupportedException();
            }

            return builder.Uri;
        }

        private HttpPeerClient CreateClient(EndPoint endPoint, bool openConnectionForEachRequest)
        {
            var baseUri = CreateBaseUri(endPoint);
            var client = handlerFactory is null
                ? new HttpPeerClient(baseUri, new SocketsHttpHandler { ConnectTimeout = connectTimeout }, true)
                : new HttpPeerClient(baseUri, handlerFactory.CreateHandler(clientHandlerName), false);

            client.DefaultRequestHeaders.ConnectionClose = openConnectionForEachRequest;
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, (GetType().Assembly.GetName().Version ?? new Version()).ToString()));
            client.Timeout = requestTimeout;
            client.SetProtocolVersion(protocolVersion);
#if !NETCOREAPP3_1
            client.DefaultVersionPolicy = protocolVersionPolicy;
#endif
            return client;
        }

        private HttpPeerClient GetOrCreatePeer(EndPoint peer)
        {
            if (!clientCache.TryGetValue(peer, out var client))
            {
                client = CreateClient(peer, false);

                // destroy client if we cannot add it to the cache
                var temp = clientCache.GetOrAdd(peer, client);
                if (!ReferenceEquals(client, temp))
                {
                    client.Dispose();
                    client = temp;
                }
            }

            return client;
        }

        HttpPeerClient? IPeerMesh<HttpPeerClient>.TryGetPeer(EndPoint peer)
            => clientCache.TryGetValue(peer, out var result) ? result : null;

        /// <summary>
        /// Starts serving HyParView messages and announces itself to the entire cluster via contact node.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        public async Task StartAsync(CancellationToken token)
        {
            if (lifetimeService is not null)
            {
                localNode ??= await lifetimeService.TryResolveLocalNodeAsync(token).ConfigureAwait(false);
                contactNode ??= await lifetimeService.TryResolveContactNodeAsync(token).ConfigureAwait(false);
                lifetimeService.OnStart(this);
            }

            // local node is required parameter
            if (localNode is null)
                throw new HyParViewProtocolException(ExceptionMessages.UnknownLocalNodeAddress);

            if (contactNode is null)
                Logger.NoContactNodeProvider();

            await StartAsync(contactNode, token).ConfigureAwait(false);
        }

        public override async Task StopAsync(CancellationToken token)
        {
            lifetimeService?.OnStop(this);
            await base.StopAsync(token).ConfigureAwait(false);
        }

        protected sealed override void Destroy(EndPoint peer)
        {
            if (clientCache.TryRemove(peer, out var client))
                client.Dispose();
        }

        protected sealed override ValueTask DestroyAsync(EndPoint peer)
        {
            var result = new ValueTask();
            try
            {
                Destroy(peer);
            }
            catch (Exception e)
            {
#if NETCOREAPP3_1
                result = new(Task.FromException(e));
#else
                result = ValueTask.FromException(e);
#endif
            }

            return result;
        }

        protected sealed override ValueTask DisconnectAsync(EndPoint peer)
        {
            var result = new ValueTask();
            try
            {
                if (clientCache.TryGetValue(peer, out var client))
                    client.CancelPendingRequests();
            }
            catch (Exception e)
            {
#if NETCOREAPP3_1
                result = new(Task.FromException(e));
#else
                result = ValueTask.FromException(e);
#endif
            }

            return result;
        }
    }
}