using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    internal sealed class WebApplicationSetup
    {
        public void Configure(IApplicationBuilder builder) { }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
        }
    }
}
