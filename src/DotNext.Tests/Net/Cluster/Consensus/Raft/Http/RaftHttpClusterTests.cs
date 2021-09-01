using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;
    using static Hosting.HostBuilderExtensions;
    using static Threading.Tasks.Synchronization;

    [ExcludeFromCodeCoverage]
    public sealed class EmbeddedClusterSupportTests : Test
    {
        private sealed class AsyncLeaderChangedEvent : DotNext.Net.Cluster.Consensus.Raft.LeaderChangedEvent, IClusterMemberLifetime
        {
            void IClusterMemberLifetime.OnStart(IRaftCluster cluster, IDictionary<string, string> metadata)
                => cluster.LeaderChanged += OnLeaderChanged;

            void IClusterMemberLifetime.OnStop(IRaftCluster cluster)
                => cluster.LeaderChanged -= OnLeaderChanged;
        }

        private sealed class LeaderChangedEvent : EventWaitHandle, IClusterMemberLifetime
        {
            internal volatile IClusterMember Leader;

            internal LeaderChangedEvent()
                : base(false, EventResetMode.ManualReset)
            {
            }

            void IClusterMemberLifetime.OnStart(IRaftCluster cluster, IDictionary<string, string> metadata)
            {
                cluster.LeaderChanged += OnLeaderChanged;
            }

            private void OnLeaderChanged(ICluster sender, IClusterMember leader)
            {
                if (leader is null)
                    return;
                Leader = leader;
                Set();
            }

            void IClusterMemberLifetime.OnStop(IRaftCluster cluster)
            {
                cluster.LeaderChanged -= OnLeaderChanged;
            }
        }

        private static IHost CreateHost<TStartup>(int port, IDictionary<string, string> configuration, IClusterMemberLifetime configurator = null)
            where TStartup : class
        {
            return new HostBuilder()
                .ConfigureWebHost(webHost => webHost.UseKestrel(options => options.ListenLocalhost(port))
                    .ConfigureServices(services =>
                    {
                        if (configurator is not null)
                            services.AddSingleton(configurator);
                    })
                    .UseStartup<TStartup>()
                )
                .UseHostOptions(new HostOptions { ShutdownTimeout = TimeSpan.FromMinutes(2) })
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
                .ConfigureLogging(static builder => builder.AddDebug().SetMinimumLevel(LogLevel.Debug))
                .JoinCluster()
                .Build();
        }

        [Fact]
        public static async Task CommunicationWithLeader()
        {
            var config1 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"members:2", "http://localhost:3264"},
                {"requestTimeout", "00:01:00"}
            };
            var config2 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"members:2", "http://localhost:3264"},
                {"requestTimeout", "00:01:00"}
            };
            var config3 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"members:2", "http://localhost:3264"},
                {"requestTimeout", "00:01:00"}
            };
            using var listener1 = new LeaderChangedEvent();
            using var listener2 = new LeaderChangedEvent();
            using var listener3 = new LeaderChangedEvent();
            using var host1 = CreateHost<Startup>(3262, config1, listener1);
            using var host2 = CreateHost<Startup>(3263, config2, listener2);
            using var host3 = CreateHost<Startup>(3264, config3, listener3);
            await host1.StartAsync();
            await host2.StartAsync();
            await host3.StartAsync();

            //ensure that leader is elected
            WaitHandle.WaitAll(new WaitHandle[] { listener1, listener2, listener3 }, DefaultTimeout);

            var box1 = host1.Services.GetServices<IInputChannel>().Where(Func.IsTypeOf<Mailbox>()).FirstOrDefault() as Mailbox;
            NotNull(box1);

            var box2 = host2.Services.GetServices<IInputChannel>().Where(Func.IsTypeOf<Mailbox>()).FirstOrDefault() as Mailbox;
            NotNull(box2);

            var box3 = host3.Services.GetServices<IInputChannel>().Where(Func.IsTypeOf<Mailbox>()).FirstOrDefault() as Mailbox;
            NotNull(box3);

            await host1.Services.GetRequiredService<IMessageBus>().LeaderRouter.SendSignalAsync(new TextMessage("Message to leader", "simple"));

            //ensure that one of the boxes is not empty
            var success = false;

            foreach (var box in new[] { box1, box2, box3 })
                if (box.TryDequeue(out var response))
                {
                    success = true;
                    Equal("Message to leader", await response.ReadAsTextAsync());
                    break;
                }

            True(success);

            await host3.StopAsync();
            await host2.StopAsync();
            await host1.StopAsync();
        }

        private static async ValueTask<StreamMessage> CreateBufferedMessageAsync(IMessage message, CancellationToken token)
        {
            var result = new StreamMessage(new MemoryStream(), false, message.Name, message.Type);
            await result.LoadFromAsync(message, token);
            return result;
        }

        [Fact]
        public static async Task MessageExchange()
        {
            var config1 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"lowerElectionTimeout", "600" },
                {"upperElectionTimeout", "900" },
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"requestTimeout", "00:01:00"}
            };
            var config2 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"lowerElectionTimeout", "600" },
                {"upperElectionTimeout", "900" },
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"requestTimeout", "00:01:00"}
            };
            using var host1 = CreateHost<Startup>(3262, config1);
            using var host2 = CreateHost<Startup>(3263, config2);
            await host1.StartAsync();
            await host2.StartAsync();

            // 3263 member expected here
            var client = default(ISubscriber);
            var messageBox = host2.Services.GetServices<IInputChannel>().Where(Func.IsTypeOf<Mailbox>()).FirstOrDefault() as Mailbox;
            NotNull(messageBox);
            //request-reply test
            var response = await client.SendTextMessageAsync<StreamMessage>(CreateBufferedMessageAsync, "Request", "Ping");
            True(response.IsReusable);
            NotNull(response);
            Equal("Reply", response.Name);
            Equal("Pong", await response.ReadAsTextAsync());

            //one-way message
            await client.SendTextSignalAsync("OneWayMessage", "Hello, world");
            True(messageBox.TryDequeue(out response));
            NotNull(response);
            Equal("Hello, world", await response.ReadAsTextAsync());

            //one-way large message ~ 1Mb
            await client.SendSignalAsync(new BinaryMessage(new byte[1024 * 1024], "OneWayMessage"), false);
            //wait for response
            for (var timeout = new Threading.Timeout(TimeSpan.FromMinutes(1)); !messageBox.TryDequeue(out response); timeout.ThrowIfExpired())
                await Task.Delay(10);
            Equal(1024 * 1024, response.As<IMessage>().Length);

            await host1.StopAsync();
            await host2.StopAsync();
        }

        [Fact]
        public static async Task TypedMessageExchange()
        {
            var config1 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"lowerElectionTimeout", "600" },
                {"upperElectionTimeout", "900" },
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"requestTimeout", "00:01:00"}
            };
            var config2 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"lowerElectionTimeout", "600" },
                {"upperElectionTimeout", "900" },
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"requestTimeout", "00:01:00"}
            };
            using var host1 = CreateHost<Startup>(3262, config1);
            using var host2 = CreateHost<Startup>(3263, config2);
            await host1.StartAsync();
            await host2.StartAsync();

            // 3263 member expected here
            var client = default(ISubscriber);
            var messageBox = host2.Services.GetServices<IInputChannel>().Where(Func.IsTypeOf<TestMessageHandler>()).FirstOrDefault() as TestMessageHandler;
            NotNull(messageBox);

            var typedClient = new MessagingClient(client);

            // duplex messages
            var result = await typedClient.SendMessageAsync<AddMessage, ResultMessage>(new() { X = 40, Y = 2 });
            Equal(42, result.Result);

            result = await typedClient.SendMessageAsync<SubtractMessage, ResultMessage>(new() { X = 40, Y = 2 });
            Equal(38, result.Result);

            // one-way message
            Equal(0, messageBox.Result);

            await typedClient.SendSignalAsync<ResultMessage>(new() { Result = 42 });
            Equal(42, messageBox.Result);

            await host1.StopAsync();
            await host2.StopAsync();
        }

        private static IRaftHttpCluster GetLocalClusterView(IHost host)
            => host.Services.GetRequiredService<IRaftHttpCluster>();

        [Fact]
        public static async Task Leadership()
        {
            var config1 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"publicEndPoint", "http://localhost:3262"},
                {"coldStart", "true"},
            };
            var config2 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"publicEndPoint", "http://localhost:3263"},
                {"coldStart", "false"}
            };
            var config3 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"publicEndPoint", "http://localhost:3264"},
                {"coldStart", "false"}
            };

            var listener = new AsyncLeaderChangedEvent();
            using var host1 = CreateHost<Startup>(3262, config1, listener);
            await host1.StartAsync();
            True(GetLocalClusterView(host1).Readiness.IsCompletedSuccessfully);

            // two nodes in frozen state
            using var host2 = CreateHost<Startup>(3263, config2);
            await host2.StartAsync();

            using var host3 = CreateHost<Startup>(3264, config3);
            await host3.StartAsync();

            True(await listener.Result.WaitAsync(DefaultTimeout));
            Equal(GetLocalClusterView(host1).LocalMemberAddress, listener.Result.Result.EndPoint);

            // add two nodes to the cluster
            True(await GetLocalClusterView(host1).AddMemberAsync(GetLocalClusterView(host2).LocalMemberId, GetLocalClusterView(host2).LocalMemberAddress));
            await GetLocalClusterView(host2).Readiness.WaitAsync(DefaultTimeout);

            True(await GetLocalClusterView(host1).AddMemberAsync(GetLocalClusterView(host3).LocalMemberId, GetLocalClusterView(host3).LocalMemberAddress));
            await GetLocalClusterView(host3).Readiness.WaitAsync(DefaultTimeout);

            Equal(GetLocalClusterView(host1).Leader.EndPoint, GetLocalClusterView(host2).Leader.EndPoint);
            Equal(GetLocalClusterView(host1).Leader.EndPoint, GetLocalClusterView(host3).Leader.EndPoint);

            await host3.StopAsync();
            await host2.StopAsync();
            await host1.StopAsync();
        }

        [Fact]
        public static async Task SingleNodeWithoutConsensus()
        {
            var config = new Dictionary<string, string>
            {
                { "partitioning", "true" },
                { "metadata:nodeName", "TestNode" },
                { "hostAddressHint", "127.0.0.1" },
                { "heartbeatThreshold", "0.3" },
                { "members:0", "http://localhost:3262" },
                { "members:1", "http://localhost:3263" }
            };
            using var leaderResetEvent = new LeaderChangedEvent();
            using var host = CreateHost<Startup>(3262, config, leaderResetEvent);
            await host.StartAsync();
            leaderResetEvent.WaitOne(TimeSpan.FromSeconds(5));
            Null(leaderResetEvent.Leader);
            await host.StopAsync();
        }

        [Fact]
        public static async Task SingleNodeWithConsensus()
        {
            var config = new Dictionary<string, string>
            {
                { "partitioning", "true" },
                { "metadata:nodeName", "TestNode" },
                { "members:0", "http://localhost:3262" }
            };
            using var leaderResetEvent = new LeaderChangedEvent();
            using var host = CreateHost<Startup>(3262, config, leaderResetEvent);
            await host.StartAsync();
            leaderResetEvent.WaitOne(DefaultTimeout);
            NotNull(leaderResetEvent.Leader);
            False(leaderResetEvent.Leader.IsRemote);
            Equal("TestNode", (await leaderResetEvent.Leader.GetMetadataAsync())["nodeName"]);
            await host.StopAsync();
        }

        [Fact]
        public static async Task DependencyInjection()
        {
            var config = new Dictionary<string, string>
            {
                { "partitioning", "false" },
                { "metadata:nodeName", "TestNode" },
                { "members:0", "http://localhost:3262" },
                { "members:1", "http://localhost:3263" },
                { "allowedNetworks:0", "127.0.0.0" }
            };
            using var host = CreateHost<Startup>(3262, config);
            await host.StartAsync();
            object service = host.Services.GetService<ICluster>();
            NotNull(service);

            Equal(2, host.Services.GetService<IPeerMesh>().Peers.Count);
            service = host.Services.GetService<IPeerMesh<IRaftClusterMember>>();
            NotNull(service);
            service = host.Services.GetService<IRaftCluster>();
            NotNull(service);
            await host.StopAsync();
        }
    }
}
