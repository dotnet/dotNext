using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Discovery.HyParView.Http;

using Buffers;
using Net.Http;

[SuppressMessage("Performance", "CA1812", Justification = "This class is instantiated by DI container")]
internal sealed partial class HttpPeerController : PeerController, IHostedService, IPeerMesh<HttpPeerClient>
{
    private const string UserAgent = "HyParView.NET";
    private readonly IHttpMessageHandlerFactory? handlerFactory;
    private readonly IPeerLifetime? lifetimeService;
    private readonly MemoryAllocator<byte>? allocator;
    private readonly string clientHandlerName;
    private readonly TimeSpan requestTimeout;
    private readonly HttpProtocolVersion protocolVersion;
    private readonly HttpVersionPolicy protocolVersionPolicy;
    private readonly ConcurrentDictionary<EndPoint, HttpPeerClient> clientCache;
    private readonly Uri resourcePath;
    private readonly IServer server;

    private readonly HttpEndPoint localNode;
    private HttpEndPoint? contactNode;

    public HttpPeerController(
        IOptions<HttpPeerConfiguration> configuration,
        ILoggerFactory loggerFactory,
        IServer server,
        IHttpMessageHandlerFactory? handlerFactory = null,
        IPeerLifetime? lifetimeService = null,
        MemoryAllocator<byte>? allocator = null)
        : base(configuration.Value)
    {
        // configuration
        clientHandlerName = configuration.Value.ClientHandlerName;
        requestTimeout = configuration.Value.RequestTimeout;
        this.allocator = configuration.Value.Allocator ?? allocator;
        protocolVersion = configuration.Value.ProtocolVersion;
        protocolVersionPolicy = configuration.Value.ProtocolVersionPolicy;
        contactNode = configuration.Value.ContactNode;
        localNode = configuration.Value.LocalNode ?? throw new HyParViewProtocolException(ExceptionMessages.UnknownLocalNodeAddress);
        resourcePath = new(configuration.Value.ResourcePath.Value is { Length: > 0 } path ? path : HttpPeerConfiguration.DefaultResourcePath, UriKind.Relative);

        // resolve dependencies
        this.handlerFactory = handlerFactory;
        this.lifetimeService = lifetimeService;
        Logger = loggerFactory.CreateLogger(GetType());
        this.server = server;

        // various init
        clientCache = new();
    }

    protected override bool IsLocalNode(EndPoint peer) => localNode.Equals(peer);

    /// <summary>
    /// Gets the logger associated with this controller.
    /// </summary>
    protected override ILogger Logger { get; }

    internal PathString ResourcePath => resourcePath.OriginalString;

    private HttpPeerClient CreateClient(HttpEndPoint endPoint, bool openConnectionForEachRequest)
    {
        var baseUri = endPoint.CreateUriBuilder().Uri;
        var client = handlerFactory is null
            ? new HttpPeerClient(baseUri, new SocketsHttpHandler { ConnectTimeout = requestTimeout }, true)
            : new HttpPeerClient(baseUri, handlerFactory.CreateHandler(clientHandlerName), false);

        client.DefaultRequestHeaders.ConnectionClose = openConnectionForEachRequest;
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, (GetType().Assembly.GetName().Version ?? new Version()).ToString()));
        client.Timeout = requestTimeout;
        client.SetProtocolVersion(protocolVersion);
        client.DefaultVersionPolicy = protocolVersionPolicy;
        return client;
    }

    private HttpPeerClient GetOrCreatePeer(HttpEndPoint peer)
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
        lifetimeService?.OnStart(this);

        // local node is required parameter
        if (contactNode is null)
            Logger.NoContactNodeProvider();

        await StartAsync(contactNode, token).ConfigureAwait(false);
    }

    public override async Task StopAsync(CancellationToken token)
    {
        lifetimeService?.OnStop(this);
        await base.StopAsync(token).ConfigureAwait(false);
    }

    protected override void Destroy(EndPoint peer)
    {
        if (clientCache.TryRemove(peer, out var client))
            client.Dispose();
    }

    protected override ValueTask DestroyAsync(EndPoint peer)
    {
        var result = ValueTask.CompletedTask;
        try
        {
            Destroy(peer);
        }
        catch (Exception e)
        {
            result = ValueTask.FromException(e);
        }

        return result;
    }

    protected override ValueTask DisconnectAsync(EndPoint peer)
    {
        var result = ValueTask.CompletedTask;
        try
        {
            if (clientCache.TryGetValue(peer, out var client))
                client.CancelPendingRequests();
        }
        catch (Exception e)
        {
            result = ValueTask.FromException(e);
        }

        return result;
    }
}