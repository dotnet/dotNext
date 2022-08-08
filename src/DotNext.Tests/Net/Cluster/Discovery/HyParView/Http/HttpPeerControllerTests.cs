using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Connections;
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
                {"localNode", "http://localhost:3362/"}
            };

            var config2 = new Dictionary<string, string>
            {
                {"lowerShufflePeriod", "10"},
                {"upperShufflePeriod", "5"},
                {"requestTimeout", "00:01:00"},
                {"localNode", "http://localhost:3363/"},
                {"contactNode", "http://localhost:3362/"},
            };

            var listener1 = new MembershipChangeEventListener();
            using var peer1 = CreateHost<Startup>(3362, config1, listener1);
            await peer1.StartAsync();

            var listener2 = new MembershipChangeEventListener();
            using var peer2 = CreateHost<Startup>(3363, config2, listener2);
            await peer2.StartAsync();

            await Task.WhenAll(listener1.DiscoveryTask, listener1.DiscoveryTask).WaitAsync(DefaultTimeout);

            Equal(new UriEndPoint(new("http://localhost:3362/", UriKind.Absolute)), listener2.DiscoveryTask.Result, EndPointFormatter.UriEndPointComparer);
            Equal(new UriEndPoint(new("http://localhost:3363/", UriKind.Absolute)), listener1.DiscoveryTask.Result, EndPointFormatter.UriEndPointComparer);

            // shutdown peer gracefully
            await peer2.StopAsync();

            Equal(new UriEndPoint(new("http://localhost:3363/", UriKind.Absolute)), await listener1.DisconnectionTask.WaitAsync(DefaultTimeout), EndPointFormatter.UriEndPointComparer);

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
                {"localNode", "http://localhost:3362/"}
            };

            using var peer1 = CreateHost<Startup>(3362, config1);
            await peer1.StartAsync();

            NotNull(peer1.Services.GetService<PeerController>());
            NotNull(peer1.Services.GetService<IPeerMesh<HttpPeerClient>>());

            await peer1.StopAsync();
        }
    }
}