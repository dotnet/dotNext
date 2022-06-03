using System.Net;
using System.Net.Http.Headers;
using System.Text;
using DotNext;
using DotNext.Net;
using DotNext.Net.Http;
using DotNext.Net.Cluster.Discovery.HyParView;
using DotNext.Net.Cluster.Discovery.HyParView.Http;
using DotNext.Net.Cluster.Messaging.Gossip;
using Microsoft.Extensions.Options;
using static System.Globalization.CultureInfo;

namespace HyParViewPeer;

internal sealed class Startup
{
    private const string SenderAddressHeader = "X-Sender-Address";
    private const string SenderIdHeader = "X-Rumor-ID";

    private const string RumorResource = "/rumor";
    private const string BroadcastResource = "/broadcast";
    private const string NeighborsResource = "/neighbors";

    private sealed class RumorSender : Disposable, IRumorSender
    {
        private readonly IPeerMesh<HttpPeerClient> mesh;
        private readonly EndPoint senderAddress;
        private readonly RumorTimestamp senderId;

        internal RumorSender(IPeerMesh<HttpPeerClient> mesh, EndPoint sender, RumorTimestamp id)
        {
            this.mesh = mesh;
            this.senderAddress = sender;
            this.senderId = id;
        }

        private async Task SendAsync(HttpPeerClient client, CancellationToken token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, BroadcastResource);
            AddSenderAddress(request.Headers, senderAddress);
            AddRumorId(request.Headers, senderId);
            using var response = await client.SendAsync(request, token);
            response.EnsureSuccessStatusCode();
        }

        Task IRumorSender.SendAsync(EndPoint peer, CancellationToken token)
        {
            var client = mesh.TryGetPeer(peer);
            return client is not null && !senderAddress.Equals(peer)
                ? SendAsync(client, token)
                : Task.CompletedTask;
        }

        public new ValueTask DisposeAsync() => base.DisposeAsync();

        private static void AddSenderAddress(HttpRequestHeaders headers, EndPoint address)
            => headers.Add(SenderAddressHeader, address.ToString());

        internal static HttpEndPoint ParseSenderAddress(HttpRequest request)
            => HttpEndPoint.TryParse(request.Headers[SenderAddressHeader], out var result) ? result : throw new FormatException("Incorrect sender address");

        private static void AddRumorId(HttpRequestHeaders headers, in RumorTimestamp id)
            => headers.Add(SenderIdHeader, id.ToString());

        internal static RumorTimestamp ParseRumorId(HttpRequest request)
            => RumorTimestamp.TryParse(request.Headers[SenderIdHeader], out var result) ? result : throw new FormatException("Invalid rumor ID");
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseHyParViewProtocolHandler().UseRouting().UseEndpoints(static endpoints =>
        {
            endpoints.MapGet(RumorResource, SendRumourAsync);
            endpoints.MapGet(NeighborsResource, PrintNeighborsAsync);
            endpoints.MapPost(BroadcastResource, BroadcastAsync);
        });
    }

    private static (EndPoint, RumorTimestamp) PrepareMessageId(IServiceProvider sp)
    {
        var config = sp.GetRequiredService<IOptions<HttpPeerConfiguration>>().Value;
        var manager = sp.GetRequiredService<RumorSpreadingManager>();
        return (config.LocalNode!, manager.Tick());
    }

    private static Task BroadcastAsync(HttpContext context)
    {
        var senderAddress = RumorSender.ParseSenderAddress(context.Request);
        var senderId = RumorSender.ParseRumorId(context.Request);

        var spreadingManager = context.RequestServices.GetRequiredService<RumorSpreadingManager>();
        if (!spreadingManager.CheckOrder(senderAddress, senderId))
            return Task.CompletedTask;

        Console.WriteLine($"Spreading rumor from {senderAddress} with sequence number = {senderId}");

        return context.RequestServices
            .GetRequiredService<PeerController>()
            .EnqueueBroadcastAsync(controller => new RumorSender((IPeerMesh<HttpPeerClient>)controller, senderAddress, senderId))
            .AsTask();
    }

    private static Task SendRumourAsync(HttpContext context)
    {
        var (sender, id) = PrepareMessageId(context.RequestServices);

        return context.RequestServices
            .GetRequiredService<PeerController>()
            .EnqueueBroadcastAsync(controller => new RumorSender((IPeerMesh<HttpPeerClient>)controller, sender, id))
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
        services.AddSingleton<RumorSpreadingManager>(static sp => new RumorSpreadingManager())
            .AddSingleton<IPeerLifetime, HyParViewPeerLifetime>()
            .AddSingleton<IHttpMessageHandlerFactory, HyParViewClientHandlerFactory>()
            .AddOptions()
            .AddRouting();
    }
}