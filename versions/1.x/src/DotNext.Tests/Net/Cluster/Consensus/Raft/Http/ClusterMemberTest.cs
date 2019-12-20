using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    [ExcludeFromCodeCoverage]
    public abstract class ClusterMemberTest : Assert
    {

        private protected static IWebHost CreateHost<TStartup>(int port, bool localhost, IDictionary<string, string> configuration, IRaftClusterConfigurator configurator = null)
            where TStartup : class
        {
            return new WebHostBuilder()
                .UseKestrel(options =>
                {
                    if (localhost)
                        options.ListenLocalhost(port);
                    else
                        options.ListenAnyIP(port);
                })
                .UseShutdownTimeout(TimeSpan.FromMinutes(2))
                .ConfigureLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug))
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
                .ConfigureServices(services =>
                {
                    if (configurator != null)
                        services.AddSingleton(configurator);
                })
                .UseStartup<TStartup>()
                .Build();
        }
    }
}
