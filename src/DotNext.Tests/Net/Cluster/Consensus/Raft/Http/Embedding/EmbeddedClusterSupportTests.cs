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
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Embedding
{
    using Messaging;
    using static DotNext.Hosting.HostBuilderExtensions;

    [ExcludeFromCodeCoverage]
    public sealed class EmbeddedClusterSupportTests : Test
    {
        private sealed class LeaderChangedEvent : EventWaitHandle, IClusterMemberLifetime
        {
            internal volatile IClusterMember Leader;

            internal LeaderChangedEvent()
                : base(false, EventResetMode.ManualReset)
            {
            }

            void IClusterMemberLifetime.Initialize(IRaftCluster cluster, IDictionary<string, string> metadata)
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

            void IClusterMemberLifetime.Shutdown(IRaftCluster cluster)
            {
                cluster.LeaderChanged -= OnLeaderChanged;
            }
        }

        private static IHost CreateHost<TStartup>(int port, bool localhost, IDictionary<string, string> configuration, IClusterMemberLifetime configurator = null, IMemberDiscoveryService discovery = null)
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
                    .ConfigureServices(services =>
                    {
                        if (configurator is not null)
                            services.AddSingleton(configurator);
                        if (discovery is not null)
                            services.AddSingleton(discovery);
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
            using var host1 = CreateHost<Startup>(3262, true, config1, listener1);
            using var host2 = CreateHost<Startup>(3263, true, config2, listener2);
            using var host3 = CreateHost<Startup>(3264, true, config3, listener3);
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
            using var host1 = CreateHost<Startup>(3262, true, config1);
            using var host2 = CreateHost<Startup>(3263, true, config2);
            await host1.StartAsync();
            await host2.StartAsync();

            var client = host1.Services.GetService<IMessageBus>().Members.FirstOrDefault(static member => ((IPEndPoint)member.EndPoint).Port == 3263);
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
            using var host1 = CreateHost<Startup>(3262, true, config1);
            using var host2 = CreateHost<Startup>(3263, true, config2);
            await host1.StartAsync();
            await host2.StartAsync();

            var client = host1.Services.GetService<IMessageBus>().Members.FirstOrDefault(static member => ((IPEndPoint)member.EndPoint).Port == 3263);
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

        [Theory]
        [InlineData(0)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(200)]
        [InlineData(300)]
        [InlineData(400)]
        [InlineData(500)]
        public static async Task Leadership(int delay)
        {
            static void CheckLeadership(IClusterMember member1, IClusterMember member2)
                => Equal(member1.EndPoint, member2.EndPoint);

            var config1 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"members:2", "http://localhost:3264"}
            };
            var config2 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"members:2", "http://localhost:3264"}
            };
            var config3 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"standby", "true"},
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"members:2", "http://localhost:3264"}
            };
            using var listener1 = new LeaderChangedEvent();
            using var listener2 = new LeaderChangedEvent();
            using var listener3 = new LeaderChangedEvent();
            using var host1 = CreateHost<Startup>(3262, true, config1, listener1);
            using var host2 = CreateHost<Startup>(3263, true, config2, listener2);
            using var host3 = CreateHost<Startup>(3264, true, config3, listener3);
            await host1.StartAsync();
            await host2.StartAsync();
            await Task.Delay(delay);
            await host3.StartAsync();

            WaitHandle.WaitAll(new WaitHandle[] { listener1, listener2, listener3 }, DefaultTimeout);

            IClusterMember leader1, leader2, leader3;

            //wait for stable election
            for (var timer = Task.Delay(2000); ; await Task.Delay(100))
            {
                if (timer.IsCompleted)
                    throw new RaftProtocolException("Leader election failed");
                leader1 = host1.Services.GetRequiredService<ICluster>().Leader;
                leader2 = host2.Services.GetRequiredService<ICluster>().Leader;
                leader3 = host3.Services.GetRequiredService<ICluster>().Leader;
                if (leader1 is null || leader2 is null || leader3 is null)
                    continue;
                if (leader1.EndPoint.Equals(leader2.EndPoint) && leader3.EndPoint.Equals(leader2.EndPoint))
                    break;
            }

            listener1.Reset();
            listener2.Reset();
            listener3.Reset();
            listener1.Leader = listener2.Leader = listener3.Leader = null;

            //let's shutdown leader node

            var removedNode = default(int?);

            if (!leader1.IsRemote)
            {
                removedNode = 1;
                await host1.StopAsync();
            }

            if (!leader2.IsRemote)
            {
                removedNode = 2;
                await host2.StopAsync();
            }

            if (!leader3.IsRemote)
            {
                removedNode = 3;
                await host3.StopAsync();
            }

            NotNull(removedNode);

            switch (removedNode)
            {
                case 1:
                    //wait for new leader
                    WaitHandle.WaitAll(new WaitHandle[] { listener2, listener3 }, DefaultTimeout);
                    NotNull(listener2.Leader);
                    NotNull(listener3.Leader);
                    CheckLeadership(listener2.Leader, listener3.Leader);
                    break;
                case 2:
                    //wait for new leader
                    WaitHandle.WaitAll(new WaitHandle[] { listener1, listener3 }, DefaultTimeout);
                    NotNull(listener1.Leader);
                    NotNull(listener3.Leader);
                    CheckLeadership(listener1.Leader, listener3.Leader);
                    break;
                case 3:
                    //wait for new leader
                    WaitHandle.WaitAll(new WaitHandle[] { listener1, listener2 }, DefaultTimeout);
                    NotNull(listener1.Leader);
                    NotNull(listener2.Leader);
                    CheckLeadership(listener1.Leader, listener2.Leader);
                    break;
                default:
                    throw new Exception();
            }
            //check metrics
            var numberOfRequests =
                (host1.Services.GetService<MetricsCollector>() as TestMetricsCollector).RequestCount +
                (host2.Services.GetService<MetricsCollector>() as TestMetricsCollector).RequestCount +
                (host3.Services.GetService<MetricsCollector>() as TestMetricsCollector).RequestCount;

            var hasLeader = (host1.Services.GetService<MetricsCollector>() as TestMetricsCollector).LeaderStateIndicator |
                (host2.Services.GetService<MetricsCollector>() as TestMetricsCollector).LeaderStateIndicator |
                (host3.Services.GetService<MetricsCollector>() as TestMetricsCollector).LeaderStateIndicator;

            var heartbeats = (host1.Services.GetService<MetricsCollector>() as TestMetricsCollector).HeartbeatCount +
                (host2.Services.GetService<MetricsCollector>() as TestMetricsCollector).HeartbeatCount +
                (host3.Services.GetService<MetricsCollector>() as TestMetricsCollector).HeartbeatCount;

            True(hasLeader);
            True(numberOfRequests > 0);
            True(heartbeats > 0);

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
            using var host = CreateHost<Startup>(3262, true, config, leaderResetEvent);
            await host.StartAsync();
            leaderResetEvent.WaitOne(TimeSpan.FromSeconds(5));
            Null(leaderResetEvent.Leader);
            await host.StopAsync();
        }

        [Fact]
        public static async Task CustomServiceDiscovery()
        {
            var config = new Dictionary<string, string>
            {
                { "partitioning", "true" },
                { "metadata:nodeName", "TestNode" },
                { "hostAddressHint", "127.0.0.1" },
                { "heartbeatThreshold", "0.3" },
            };
            using var discovery = new TestDiscoveryService()
            {
                new Uri("http://localhost:3262"),
                new Uri("http://localhost:3263")
            };

            using var leaderResetEvent = new LeaderChangedEvent();
            using var host = CreateHost<Startup>(3262, true, config, leaderResetEvent, discovery: discovery);
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
            using var host = CreateHost<Startup>(3262, true, config, leaderResetEvent);
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
            using var host = CreateHost<Startup>(3262, true, config);
            await host.StartAsync();
            object service = host.Services.GetService<ICluster>();
            NotNull(service);
            var count = 0;
            foreach (var member in host.Services.GetService<ICluster>().Members)
                if (!member.IsRemote)
                    count += 1;
            Equal(1, count);
            service = host.Services.GetService<IExpandableCluster>();
            NotNull(service);
            service = host.Services.GetService<IRaftCluster>();
            NotNull(service);
            await host.StopAsync();
        }
    }
}
