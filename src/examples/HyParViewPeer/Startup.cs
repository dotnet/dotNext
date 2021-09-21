using System.Text;
using DotNext.Net;
using DotNext.Net.Http;
using DotNext.Net.Cluster.Discovery.HyParView;
using DotNext.Net.Cluster.Discovery.HyParView.Http;

namespace HyParViewPeer;

internal sealed class Startup
{
    public void Configure(IApplicationBuilder app)
    {
        const string RumourResource = "/rumour";
        const string NeighborsResource = "/neighbors";

        app.UseHyParViewProtocolHandler().UseRouting().UseEndpoints(static endpoints =>
        {
            endpoints.MapGet(RumourResource, SendRumourAsync);
            endpoints.MapGet(NeighborsResource, PrintNeighborsAsync);
        });
    }

    private static Task SendRumourAsync(HttpContext context)
    {
        return Task.CompletedTask;
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
        services.AddSingleton<IPeerLifetime, HyParViewPeerLifetime>()
            .AddSingleton<IHttpMessageHandlerFactory, HyParViewClientHandlerFactory>()
            .AddOptions()
            .AddRouting();
    }
}