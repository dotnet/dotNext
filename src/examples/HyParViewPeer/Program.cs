using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using DotNext;
using DotNext.Net;
using DotNext.Net.Cluster.Discovery.HyParView;
using DotNext.Net.Cluster.Discovery.HyParView.Http;
using DotNext.Net.Cluster.Messaging.Gossip;
using DotNext.Net.Http;
using HyParViewPeer;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Options;

int port;
int? contactNodePort = null;

switch (args.Length)
{
    default:
        Console.WriteLine("Port number is not specified");
        return;
    case 1:
        port = int.Parse(args[0]);
        break;
    case 2:
        port = int.Parse(args[0]);
        contactNodePort = int.Parse(args[1]);
        break;
}

Console.WriteLine("Starting node...");

var configuration = new Dictionary<string, string?>
{
    {"lowerShufflePeriod", "1000"},
    {"upperShufflePeriod", "5000"},
    {"activeViewCapacity", "3"},
    {"passiveViewCapacity", "6"},
    {"requestTimeout", "00:00:30"},
    {"localNode", $"https://localhost:{port}/"}
};

if (contactNodePort.HasValue)
    configuration.Add("contactNode", $"https://localhost:{contactNodePort.GetValueOrDefault()}");

var builder = WebApplication.CreateSlimBuilder();
builder.Configuration.AddInMemoryCollection(configuration);

// web server
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(port, static listener => listener.UseHttps(LoadCertificate()));
});

// services
builder.Services
    .AddSingleton<RumorSpreadingManager>(static sp => new RumorSpreadingManager(EndPointFormatter.UriEndPointComparer))
    .AddSingleton<IPeerLifetime, HyParViewPeerLifetime>()
    .AddSingleton<IHttpMessageHandlerFactory, HyParViewClientHandlerFactory>();

// misc
builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Debug);
builder.JoinMesh();

await using var app = builder.Build();

// endpoints
app.UseHyParViewProtocolHandler().UseRouting().UseEndpoints(static endpoints =>
{
    endpoints.MapGet(RumorSender.RumorResource, SendRumourAsync);
    endpoints.MapGet(RumorSender.NeighborsResource, PrintNeighborsAsync);
    endpoints.MapPost(RumorSender.BroadcastResource, BroadcastAsync);
});

await app.RunAsync();

static X509Certificate2 LoadCertificate()
{
    using var rawCertificate = Assembly.GetCallingAssembly().GetManifestResourceStream(typeof(Program), "node.pfx");
    using var ms = new MemoryStream(1024);
    rawCertificate?.CopyTo(ms);
    ms.Seek(0, SeekOrigin.Begin);
    return new X509Certificate2(ms.ToArray(), "1234");
}

static (Uri, RumorTimestamp) PrepareMessageId(IServiceProvider sp)
{
    var config = sp.GetRequiredService<IOptions<HttpPeerConfiguration>>().Value;
    var manager = sp.GetRequiredService<RumorSpreadingManager>();
    return (config.LocalNode!, manager.Tick());
}

static Task BroadcastAsync(HttpContext context)
{
    var senderAddress = RumorSender.ParseSenderAddress(context.Request);
    var senderId = RumorSender.ParseRumorId(context.Request);

    var spreadingManager = context.RequestServices.GetRequiredService<RumorSpreadingManager>();
    if (!spreadingManager.CheckOrder(new UriEndPoint(senderAddress), senderId))
        return Task.CompletedTask;

    Console.WriteLine($"Spreading rumor from {senderAddress} with sequence number = {senderId}");

    return context.RequestServices
        .GetRequiredService<PeerController>()
        .EnqueueBroadcastAsync(controller => new RumorSender((IPeerMesh<HttpPeerClient>)controller, senderAddress, senderId))
        .AsTask();
}

static Task SendRumourAsync(HttpContext context)
{
    var (sender, id) = PrepareMessageId(context.RequestServices);

    return context.RequestServices
        .GetRequiredService<PeerController>()
        .EnqueueBroadcastAsync(controller => new RumorSender((IPeerMesh<HttpPeerClient>)controller, sender, id))
        .AsTask();
}

static Task PrintNeighborsAsync(HttpContext context)
{
    var mesh = context.RequestServices.GetRequiredService<IPeerMesh<HttpPeerClient>>();
    var sb = new StringBuilder();

    foreach (var peer in mesh.Peers)
        sb.AppendLine(peer.ToString());

    return context.Response.WriteAsync(sb.ToString(), context.RequestAborted);
}

file sealed class RumorSender : Disposable, IRumorSender
{
    private const string SenderAddressHeader = "X-Sender-Address";
    private const string SenderIdHeader = "X-Rumor-ID";

    internal const string RumorResource = "/rumor";
    internal const string BroadcastResource = "/broadcast";
    internal const string NeighborsResource = "/neighbors";
    
    private readonly IPeerMesh<HttpPeerClient> mesh;
    private readonly Uri senderAddress;
    private readonly RumorTimestamp senderId;

    internal RumorSender(IPeerMesh<HttpPeerClient> mesh, Uri sender, RumorTimestamp id)
    {
        this.mesh = mesh;
        senderAddress = sender;
        senderId = id;
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
        return client is not null && !EndPointFormatter.UriEndPointComparer.Equals(new UriEndPoint(senderAddress), peer)
            ? SendAsync(client, token)
            : Task.CompletedTask;
    }

    public new ValueTask DisposeAsync() => base.DisposeAsync();

    private static void AddSenderAddress(HttpRequestHeaders headers, Uri address)
        => headers.Add(SenderAddressHeader, address.ToString());

    internal static Uri ParseSenderAddress(HttpRequest request)
        => new(request.Headers[SenderAddressHeader]!, UriKind.Absolute);

    private static void AddRumorId(HttpRequestHeaders headers, in RumorTimestamp id)
        => headers.Add(SenderIdHeader, id.ToString());

    internal static RumorTimestamp ParseRumorId(HttpRequest request)
        => RumorTimestamp.TryParse(request.Headers[SenderIdHeader], out var result) ? result : throw new FormatException("Invalid rumor ID");
}