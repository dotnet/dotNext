using System.Net;
using System.Text;
using DotNext;
using DotNext.Net;
using DotNext.Net.Http;
using DotNext.Net.Cluster.Discovery.HyParView;
using DotNext.Net.Cluster.Discovery.HyParView.Http;
using DotNext.Net.Cluster.Messaging.Gossip;

namespace HyParViewPeer;

internal sealed class Startup
{
    private const string MessageIdHeader = "x-Message-Id";
    private const string RumourResource = "/rumour";
    private const string BroadcastResource = "/broadcast";
    private const string NeighborsResource = "/neighbors";

    private sealed class RumorSender : Disposable, IRumorSender
    {
        private readonly IPeerMesh<HttpPeerClient> mesh;
        private readonly string messageId;

        internal RumorSender(IPeerMesh<HttpPeerClient> mesh, string? messageId = null)
        {
            this.mesh = mesh;
            this.messageId = messageId ?? Guid.NewGuid().ToString();
        }

        async Task IRumorSender.SendAsync(EndPoint peer, CancellationToken token)
        {
            var client = mesh.TryGetPeer(peer);
            if (client is not null)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, BroadcastResource);
                request.Headers.Add(MessageIdHeader, messageId);
                using var response = await client.SendAsync(request, token);
                response.EnsureSuccessStatusCode();
            }
        }

        public new ValueTask DisposeAsync() => base.DisposeAsync();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseHyParViewProtocolHandler().UseRouting().UseEndpoints(static endpoints =>
        {
            endpoints.MapGet(RumourResource, SendRumourAsync);
            endpoints.MapGet(NeighborsResource, PrintNeighborsAsync);
            endpoints.MapPost(BroadcastResource, BroadcastAsync);
        });
    }

    private static Task BroadcastAsync(HttpContext context)
    {
        var detector = context.RequestServices.GetRequiredService<DuplicateRequestDetector>();
        var messageId = context.Request.Headers[MessageIdHeader];
        if (detector.IsDuplicated(messageId))
            return Task.CompletedTask;

        Console.WriteLine($"Spreading rumour with id = {messageId}");

        return context.RequestServices
            .GetRequiredService<PeerController>()
            .EnqueueBroadcastAsync(controller => new RumorSender((IPeerMesh<HttpPeerClient>)controller, messageId))
            .AsTask();
    }

    private static Task SendRumourAsync(HttpContext context)
    {
        return context.RequestServices
            .GetRequiredService<PeerController>()
            .EnqueueBroadcastAsync(static controller => new RumorSender((IPeerMesh<HttpPeerClient>)controller))
            .AsTask();
    }

    private static Task PrintNeighborsAsync(HttpContext context)
    {
        var mesh = context.RequestServices.GetRequiredService<IPeerMesh<HttpPeerClient>>();
        var sb = new StringBuilder();

        foreach (var peer in mesh.Peers)
            sb.AppendLine(peer.ToString());

        return context.Response.WriteAsync(sb.ToString(), context.RequestAborted);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<DuplicateRequestDetector>()
            .AddSingleton<IPeerLifetime, HyParViewPeerLifetime>()
            .AddSingleton<IHttpMessageHandlerFactory, HyParViewClientHandlerFactory>()
            .AddOptions()
            .AddRouting();
    }
}