using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Embedding
{
    using Messaging;

    [ExcludeFromCodeCoverage]
    public sealed class EmbeddedClusterSupportTests : ClusterMemberTest
    {
        private sealed class LeaderChangedEvent : EventWaitHandle, IRaftClusterConfigurator
        {
            internal volatile IClusterMember Leader;

            internal LeaderChangedEvent()
                : base(false, EventResetMode.ManualReset)
            {
            }

            void IRaftClusterConfigurator.Initialize(IRaftCluster cluster, IDictionary<string, string> metadata)
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

            void IRaftClusterConfigurator.Shutdown(IRaftCluster cluster)
            {
                cluster.LeaderChanged -= OnLeaderChanged;
            }
        }

        [Fact]
        public static async Task CommunicationWithLeader()
        {
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
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"members:2", "http://localhost:3264"}
            };
            using (var listener1 = new LeaderChangedEvent())
            using (var listener2 = new LeaderChangedEvent())
            using (var listener3 = new LeaderChangedEvent())
            using (var host1 = CreateHost<Startup>(3262, true, config1, listener1))
            using (var host2 = CreateHost<Startup>(3263, true, config2, listener2))
            using (var host3 = CreateHost<Startup>(3264, true, config3, listener3))
            {
                await host1.StartAsync();
                await host2.StartAsync();
                await host3.StartAsync();

                //ensure that leader is elected
                WaitHandle.WaitAll(new WaitHandle[] { listener1, listener2, listener3 });

                var box1 = host1.Services.GetRequiredService<IMessageHandler>() as Mailbox;
                var box2 = host2.Services.GetRequiredService<IMessageHandler>() as Mailbox;
                var box3 = host3.Services.GetRequiredService<IMessageHandler>() as Mailbox;


                await host1.Services.GetRequiredService<IMessageBus>().SendSignalToLeaderAsync(new TextMessage("Message to leader", "simple"));

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
                {"members:1", "http://localhost:3263"}
            };
            var config2 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"lowerElectionTimeout", "600" },
                {"upperElectionTimeout", "900" },
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"}
            };
            using (var host1 = CreateHost<Startup>(3262, true, config1))
            using (var host2 = CreateHost<Startup>(3263, true, config2))
            {
                await host1.StartAsync();
                await host2.StartAsync();

                var client = host1.Services.GetService<IMessageBus>().Members.FirstOrDefault(member => member.Endpoint.Port == 3263);
                var messageBox = host2.Services.GetService<IMessageHandler>() as Mailbox;
                NotNull(messageBox);
                //request-reply test
                var response = await client.SendTextMessageAsync(StreamMessage.CreateBufferedMessageAsync, "Request", "Ping");
                True(response.IsReusable);
                NotNull(response);
                Equal("Reply", response.Name);
                Equal("Pong", await response.ReadAsTextAsync());

                //one-way message
                await client.SendTextSignalAsync("OneWayMessage", "Hello, world");
                True(messageBox.TryDequeue(out response));
                NotNull(response);
                Equal("Hello, world", await response.ReadAsTextAsync());

                await host1.StopAsync();
                await host2.StopAsync();
            }
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
            void CheckLeadership(IClusterMember member1, IClusterMember member2)
                => Equal(member1.Endpoint, member2.Endpoint);

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
                {"members:0", "http://localhost:3262"},
                {"members:1", "http://localhost:3263"},
                {"members:2", "http://localhost:3264"}
            };
            using (var listener1 = new LeaderChangedEvent())
            using (var listener2 = new LeaderChangedEvent())
            using (var listener3 = new LeaderChangedEvent())
            using (var host1 = CreateHost<Startup>(3262, true, config1, listener1))
            using (var host2 = CreateHost<Startup>(3263, true, config2, listener2))
            using (var host3 = CreateHost<Startup>(3264, true, config3, listener3))
            {
                await host1.StartAsync();
                await host2.StartAsync();
                await Task.Delay(delay);
                await host3.StartAsync();

                WaitHandle.WaitAll(new WaitHandle[] { listener1, listener2, listener3 });

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
                    if (leader1.Endpoint.Equals(leader2.Endpoint) && leader1.Endpoint.Equals(leader2.Endpoint))
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
                        WaitHandle.WaitAll(new WaitHandle[] { listener2, listener3 });
                        NotNull(listener2.Leader);
                        NotNull(listener3.Leader);
                        CheckLeadership(listener2.Leader, listener3.Leader);
                        break;
                    case 2:
                        //wait for new leader
                        WaitHandle.WaitAll(new WaitHandle[] { listener1, listener3 });
                        NotNull(listener1.Leader);
                        NotNull(listener3.Leader);
                        CheckLeadership(listener1.Leader, listener3.Leader);
                        break;
                    case 3:
                        //wait for new leader
                        WaitHandle.WaitAll(new WaitHandle[] { listener1, listener2 });
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
            using (var leaderResetEvent = new LeaderChangedEvent())
            using (var host = CreateHost<Startup>(3262, true, config, leaderResetEvent))
            {
                await host.StartAsync();
                leaderResetEvent.WaitOne(2000);
                Null(leaderResetEvent.Leader);
                await host.StopAsync();
            }
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
            using (var leaderResetEvent = new LeaderChangedEvent())
            using (var host = CreateHost<Startup>(3262, true, config, leaderResetEvent))
            {
                await host.StartAsync();
                leaderResetEvent.WaitOne(2000);
                NotNull(leaderResetEvent.Leader);
                False(leaderResetEvent.Leader.IsRemote);
                Equal("TestNode", (await leaderResetEvent.Leader.GetMetadata())["nodeName"]);
                await host.StopAsync();
            }
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
            using (var host = CreateHost<Startup>(3262, true, config))
            {
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
}
