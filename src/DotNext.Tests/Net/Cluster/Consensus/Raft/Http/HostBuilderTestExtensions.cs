using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal static class HostBuilderTestExtensions
    {
        internal static IHostBuilder UseShutdownTimeout(this IHostBuilder builder, TimeSpan timeout)
            => builder.ConfigureServices((context, services) => 
            {
                services.Configure<HostOptions>(options =>
                {
                    options.ShutdownTimeout = timeout;
                });
            });
    }
}