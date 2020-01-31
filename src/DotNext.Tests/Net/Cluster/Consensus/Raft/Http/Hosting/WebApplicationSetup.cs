using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    [ExcludeFromCodeCoverage]
    internal sealed class WebApplicationSetup
    {
        public void Configure(IApplicationBuilder builder) { }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
        }
    }
}
