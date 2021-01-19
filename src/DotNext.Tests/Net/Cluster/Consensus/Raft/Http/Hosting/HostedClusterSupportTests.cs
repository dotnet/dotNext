using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    using IMessageBus = Messaging.IMessageBus;
    using IReplicationCluster = Replication.IReplicationCluster;
    using static DotNext.Hosting.HostBuilderExtensions;

    [ExcludeFromCodeCoverage]
    public sealed class HostedClusterSupportTests : Test
    {
        private static IHost CreateHost<TStartup>(int port, bool localhost, IDictionary<string, string> configuration, IClusterMemberLifetime configurator = null)
            where TStartup : class
        {
            return new HostBuilder()
                .ConfigureWebHost(webHost =>
                    webHost.UseKestrel(options =>
                    {
                        if (localhost)
                            options.ListenLocalhost(port);
                        else
                            options.ListenAnyIP(port);
                    })
                    .ConfigureLogging(static builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug))
                    .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
                    .ConfigureServices(services =>
                    {
                        if (configurator != null)
                            services.AddSingleton(configurator);
                    })
                    .UseStartup<TStartup>()
                )
                .UseHostOptions(new HostOptions { ShutdownTimeout = TimeSpan.FromMinutes(2) })
                .JoinCluster()
                .Build();
        }

        [Fact]
        public static async Task DependencyInjection()
        {
            var config = new Dictionary<string, string>()
            {
                {"partitioning", "false"},
                {"metadata:nodeName", "TestNode"},
                {"port", "3565"},
                {"members:0", "http://localhost:3565"},
                {"members:1", "http://localhost:3566"},
                {"allowedNetworks:0", "127.0.0.0"},
                {"keepAliveTimeout", "00:01:00"}
            };
            using var host = CreateHost<WebApplicationSetup>(3100, true, config);
            await host.StartAsync();
            object service = host.Services.GetService<ICluster>();
            NotNull(service);
            //check whether the local member present
            var count = 0;
            foreach (var member in host.Services.GetService<ICluster>().Members)
                if (!member.IsRemote)
                    count += 1;
            Equal(1, count);
            service = host.Services.GetService<IExpandableCluster>();
            NotNull(service);
            service = host.Services.GetService<IMessageBus>();
            NotNull(service);
            service = host.Services.GetService<IReplicationCluster>();
            NotNull(service);
            service = host.Services.GetService<IRaftCluster>();
            NotNull(service);
            await host.StopAsync();
        }
    }
}
