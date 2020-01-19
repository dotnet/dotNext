using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Embedding
{
    using IDistributedApplicationEnvironment = DistributedServices.IDistributedApplicationEnvironment;

    [ExcludeFromCodeCoverage]
    public sealed class DistributedServicesTests : Assert
    {
        private static IHost CreateHost<TStartup>(int port, bool localhost, IDictionary<string, string> configuration)
            where TStartup : class
        {
            return new HostBuilder()
                .ConfigureWebHost(webHost => webHost.UseKestrel(options =>
                {
                    if (localhost)
                        options.ListenLocalhost(port);
                    else
                        options.ListenAnyIP(port);
                })
                    .UseShutdownTimeout(TimeSpan.FromMinutes(2))
                    .ConfigureServices(services =>
                    {
                        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                        services.EnableDistributedServices(dir, 4);
                    })
                    .UseStartup<TStartup>()
                )
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
                .ConfigureLogging(builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug))
                .JoinCluster()
                .Build();
        }

        [Fact]
        public static async Task AcquireRelease()
        {
            var config1 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"members:2", "http://localhost:3264"},
                {"lowerElectionTimeout", "500" },
                {"upperElectionTimeout", "1000" },
            };
            var config2 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"members:2", "http://localhost:3264"},
                {"lowerElectionTimeout", "500" },
                {"upperElectionTimeout", "1000" },
            };
            var config3 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"members:2", "http://localhost:3264"},
                {"lowerElectionTimeout", "500" },
                {"upperElectionTimeout", "1000" },
            };
            using var host1 = CreateHost<Startup>(3262, true, config1);
            using var host2 = CreateHost<Startup>(3263, true, config2);
            using var host3 = CreateHost<Startup>(3264, true, config3);
            await host1.StartAsync();
            await host2.StartAsync();
            await host3.StartAsync();
            //wait for leadership
            var cluster1 = host1.Services.GetRequiredService<IDistributedApplicationEnvironment>();
            var cluster2 = host2.Services.GetRequiredService<IDistributedApplicationEnvironment>();
            var cluster3 = host3.Services.GetRequiredService<IDistributedApplicationEnvironment>();
            while(cluster1.Leader is null || 
                !object.Equals(cluster1.Leader?.Endpoint, cluster2.Leader?.Endpoint) || 
                !object.Equals(cluster1.Leader?.Endpoint, cluster3.Leader?.Endpoint) ||
                !object.Equals(cluster2.Leader?.Endpoint, cluster3.Leader?.Endpoint))
            {
                await Task.Delay(20);
            }
            await using(await cluster1.LockProvider["lock1"].AcquireAsync(TimeSpan.FromMinutes(10)))
            {
                //attempts to acquire the same lock
                var holder = await cluster2.LockProvider["lock1"].TryAcquireAsync(TimeSpan.FromSeconds(2));
                if(holder) throw new Xunit.Sdk.XunitException();
                await ThrowsAsync<TimeoutException>(() => cluster2.LockProvider["lock1"].AcquireAsync(TimeSpan.FromSeconds(2)));
            }

            await host3.StopAsync();
            await host2.StopAsync();
            await host1.StopAsync();
        }
    }
}