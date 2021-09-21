using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Discovery.HyParView.Http
{
    using HttpPeerClient = Net.Http.HttpPeerClient;

    [ExcludeFromCodeCoverage]
    public sealed class HttpPeerControllerTests : Test
    {
        private static IHost CreateHost<TStartup>(int port, IDictionary<string, string> configuration, IPeerLifetime lifetime = null)
            where TStartup : class
        {
            return new HostBuilder()
                .ConfigureWebHost(webHost => webHost.UseKestrel(options => options.ListenLocalhost(port))
                    .ConfigureServices(services =>
                    {
                        if (lifetime is not null)
                            services.AddSingleton(lifetime);
                    })
                    .UseStartup<TStartup>()
                )
                .ConfigureHostOptions(static options => options.ShutdownTimeout = DefaultTimeout)
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
                .ConfigureLogging(static builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug))
                .JoinMesh()
                .Build();
        }

        [Fact]
        public static async Task ConnectDisconnect()
        {
            var config1 = new Dictionary<string, string>
            {
                {"lowerShufflePeriod", "10"},
                {"upperShufflePeriod", "5"},
                {"requestTimeout", "00:01:00"},
                {"localNode", "http://localhost:3262/"}
            };

            var config2 = new Dictionary<string, string>
            {
                {"lowerShufflePeriod", "10"},
                {"upperShufflePeriod", "5"},
                {"requestTimeout", "00:01:00"},
                {"localNode", "http://localhost:3263/"},
                {"contactNode", "http://localhost:3262/"},
            };

            var listener1 = new MembershipChangeEventListener();
            using var peer1 = CreateHost<Startup>(3262, config1, listener1);
            await peer1.StartAsync();

            var listener2 = new MembershipChangeEventListener();
            using var peer2 = CreateHost<Startup>(3263, config2, listener2);
            await peer2.StartAsync();

            await Task.WhenAll(listener1.DiscoveryTask, listener1.DiscoveryTask).WaitAsync(DefaultTimeout);

            Equal(new HttpEndPoint("localhost", 3262, false), listener2.DiscoveryTask.Result);
            Equal(new HttpEndPoint("localhost", 3263, false), listener1.DiscoveryTask.Result);

            // shutdown peer gracefully
            await peer2.StopAsync();

            Equal(new HttpEndPoint("localhost", 3263, false), await listener1.DisconnectionTask.WaitAsync(DefaultTimeout));

            await peer1.StopAsync();
        }

        [Fact]
        public static async Task DependencyInjection()
        {
            var config1 = new Dictionary<string, string>
            {
                {"lowerShufflePeriod", "10"},
                {"upperShufflePeriod", "5"},
                {"requestTimeout", "00:01:00"},
                {"localNode", "http://localhost:3262/"}
            };

            using var peer1 = CreateHost<Startup>(3262, config1);
            await peer1.StartAsync();

            NotNull(peer1.Services.GetService<PeerController>());
            NotNull(peer1.Services.GetService<IPeerMesh<HttpPeerClient>>());

            await peer1.StopAsync();
        }
    }
}