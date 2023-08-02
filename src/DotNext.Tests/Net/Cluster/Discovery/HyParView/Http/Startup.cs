using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Discovery.HyParView.Http;

[ExcludeFromCodeCoverage]
internal sealed class Startup
{
    public void Configure(IApplicationBuilder app)
    {
        app.UseHyParViewProtocolHandler();
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions();
    }
}