using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Messaging;
    using Replication;
    using HttpEndPoint = Net.Http.HttpEndPoint;

    [ExcludeFromCodeCoverage]
    [Collection(TestCollections.Raft)]
    public sealed class RaftHttpClusterTests : Test
    {
        private sealed class LeaderTracker : LeaderChangedEvent, IClusterMemberLifetime
        {
            void IClusterMemberLifetime.OnStart(IRaftCluster cluster, IDictionary<string, string> metadata)
                => cluster.LeaderChanged += OnLeaderChanged;

            void IClusterMemberLifetime.OnStop(IRaftCluster cluster)
                => cluster.LeaderChanged -= OnLeaderChanged;
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
                .ConfigureHostOptions(static options => options.ShutdownTimeout = DefaultTimeout)
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
                {"publicEndPoint", "http://localhost:3262"},
                {"coldStart", "true"},
                {"requestTimeout", "00:01:00"}
            };

            var config2 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"publicEndPoint", "http://localhost:3263"},
                {"coldStart", "false"},
                {"requestTimeout", "00:01:00"}
            };

            var config3 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"publicEndPoint", "http://localhost:3264"},
                {"coldStart", "false"},
                {"requestTimeout", "00:01:00"}
            };

            var listener = new LeaderTracker();
            using var host1 = CreateHost<Startup>(3262, config1, listener);
            await host1.StartAsync();
            True(GetLocalClusterView(host1).Readiness.IsCompletedSuccessfully);

            // two nodes in frozen state
            using var host2 = CreateHost<Startup>(3263, config2);
            await host2.StartAsync();

            using var host3 = CreateHost<Startup>(3264, config3);
            await host3.StartAsync();

            await listener.Result.WaitAsync(DefaultTimeout);
            Equal(GetLocalClusterView(host1).LocalMemberAddress, listener.Result.Result.EndPoint);

            // add two nodes to the cluster
            True(await GetLocalClusterView(host1).AddMemberAsync(GetLocalClusterView(host2).LocalMemberId, GetLocalClusterView(host2).LocalMemberAddress));
            await GetLocalClusterView(host2).Readiness.WaitAsync(DefaultTimeout);

            True(await GetLocalClusterView(host1).AddMemberAsync(GetLocalClusterView(host3).LocalMemberId, GetLocalClusterView(host3).LocalMemberAddress));
            await GetLocalClusterView(host3).Readiness.WaitAsync(DefaultTimeout);

            var box1 = host1.Services.GetServices<IInputChannel>().Where(Func.IsTypeOf<Mailbox>()).FirstOrDefault() as Mailbox;
            NotNull(box1);

            var box2 = host2.Services.GetServices<IInputChannel>().Where(Func.IsTypeOf<Mailbox>()).FirstOrDefault() as Mailbox;
            NotNull(box2);

            var box3 = host3.Services.GetServices<IInputChannel>().Where(Func.IsTypeOf<Mailbox>()).FirstOrDefault() as Mailbox;
            NotNull(box3);

            await GetLocalClusterView(host1).LeaderRouter.SendSignalAsync(new TextMessage("Message to leader", "simple"));

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
                {"publicEndPoint", "http://localhost:3262"},
                {"coldStart", "true"},
                {"requestTimeout", "00:01:00"}
            };

            var config2 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"lowerElectionTimeout", "600" },
                {"upperElectionTimeout", "900" },
                {"publicEndPoint", "http://localhost:3263"},
                {"coldStart", "false"},
                {"standby", "true"},
                {"requestTimeout", "00:01:00"}
            };

            var listener = new LeaderTracker();
            using var host1 = CreateHost<Startup>(3262, config1, listener);
            await host1.StartAsync();

            using var host2 = CreateHost<Startup>(3263, config2);
            await host2.StartAsync();

            await listener.Result.WaitAsync(DefaultTimeout);
            Equal(GetLocalClusterView(host1).LocalMemberAddress, listener.Result.Result.EndPoint);

            True(await GetLocalClusterView(host1).AddMemberAsync(GetLocalClusterView(host2).LocalMemberId, GetLocalClusterView(host2).LocalMemberAddress));
            await GetLocalClusterView(host2).Readiness.WaitAsync(DefaultTimeout);

            var client = GetLocalClusterView(host1).As<IMessageBus>().Members.First(static s => s.EndPoint is HttpEndPoint { Port: 3263 });
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
                {"publicEndPoint", "http://localhost:3262"},
                {"coldStart", "true"},
                {"requestTimeout", "00:01:00"}
            };

            var config2 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"lowerElectionTimeout", "600" },
                {"upperElectionTimeout", "900" },
                {"publicEndPoint", "http://localhost:3263"},
                {"coldStart", "false"},
                {"standby", "true"},
                {"requestTimeout", "00:01:00"}
            };

            var listener = new LeaderTracker();
            using var host1 = CreateHost<Startup>(3262, config1, listener);
            await host1.StartAsync();

            using var host2 = CreateHost<Startup>(3263, config2);
            await host2.StartAsync();

            await listener.Result.WaitAsync(DefaultTimeout);
            Equal(GetLocalClusterView(host1).LocalMemberAddress, listener.Result.Result.EndPoint);

            True(await GetLocalClusterView(host1).AddMemberAsync(GetLocalClusterView(host2).LocalMemberId, GetLocalClusterView(host2).LocalMemberAddress));
            await GetLocalClusterView(host2).Readiness.WaitAsync(DefaultTimeout);

            var client = GetLocalClusterView(host1).As<IMessageBus>().Members.First(static s => s.EndPoint is HttpEndPoint { Port: 3263 });
            var messageBox = host2.Services.GetServices<IInputChannel>().Where(Func.IsTypeOf<TestMessageHandler>()).FirstOrDefault() as TestMessageHandler;
            NotNull(messageBox);

            var typedClient = new MessagingClient(client)
                .RegisterMessage<AddMessage>(AddMessage.Name)
                .RegisterMessage<SubtractMessage>(SubtractMessage.Name)
                .RegisterMessage<ResultMessage>(ResultMessage.Name);

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
                {"metadata:nodeName", "node1"}
            };
            var config2 = new Dictionary<string, string>
            {
                {"partitioning", "false" },
                {"publicEndPoint", "http://localhost:3263"},
                {"coldStart", "false"},
                {"metadata:nodeName", "node2"}
            };
            var config3 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"publicEndPoint", "http://localhost:3264"},
                {"coldStart", "false"},
                {"metadata:nodeName", "node3"}
            };

            var listener = new LeaderTracker();
            using var host1 = CreateHost<Startup>(3262, config1, listener);
            await host1.StartAsync();
            True(GetLocalClusterView(host1).Readiness.IsCompletedSuccessfully);

            // two nodes in frozen state
            using var host2 = CreateHost<Startup>(3263, config2);
            await host2.StartAsync();

            using var host3 = CreateHost<Startup>(3264, config3);
            await host3.StartAsync();

            await listener.Result.WaitAsync(DefaultTimeout);
            Equal(GetLocalClusterView(host1).LocalMemberAddress, listener.Result.Result.EndPoint);

            // add two nodes to the cluster
            True(await GetLocalClusterView(host1).AddMemberAsync(GetLocalClusterView(host2).LocalMemberId, GetLocalClusterView(host2).LocalMemberAddress));
            await GetLocalClusterView(host2).Readiness.WaitAsync(DefaultTimeout);

            True(await GetLocalClusterView(host1).AddMemberAsync(GetLocalClusterView(host3).LocalMemberId, GetLocalClusterView(host3).LocalMemberAddress));
            await GetLocalClusterView(host3).Readiness.WaitAsync(DefaultTimeout);

            Equal(GetLocalClusterView(host1).Leader.EndPoint, GetLocalClusterView(host2).Leader.EndPoint);
            Equal(GetLocalClusterView(host1).Leader.EndPoint, GetLocalClusterView(host3).Leader.EndPoint);

            foreach (var member in GetLocalClusterView(host1).As<IRaftCluster>().Members)
            {
                if (member.IsRemote)
                {
                    NotEmpty(await member.GetMetadataAsync());
                }
            }

            await host3.StopAsync();
            await host2.StopAsync();
            await host1.StopAsync();
        }

        [Fact]
        public static async Task ClusterRecovery()
        {
            var configRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            var config1 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"publicEndPoint", "http://localhost:3262"},
                {"coldStart", "true"},
                {"metadata:nodeName", "node1"},
                {Startup.PersistentConfigurationPath, Path.Combine(configRoot, "node1")}
            };
            var config2 = new Dictionary<string, string>
            {
                {"partitioning", "false" },
                {"publicEndPoint", "http://localhost:3263"},
                {"coldStart", "false"},
                {"metadata:nodeName", "node2"},
                {Startup.PersistentConfigurationPath, Path.Combine(configRoot, "node2")}
            };
            var config3 = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"publicEndPoint", "http://localhost:3264"},
                {"coldStart", "false"},
                {"metadata:nodeName", "node3"},
                {Startup.PersistentConfigurationPath, Path.Combine(configRoot, "node3")}
            };

            var listener = new LeaderTracker();
            using (var host1 = CreateHost<Startup>(3262, config1, listener))
            {
                await host1.StartAsync();
                True(GetLocalClusterView(host1).Readiness.IsCompletedSuccessfully);

                // two nodes in frozen state
                using var host2 = CreateHost<Startup>(3263, config2);
                await host2.StartAsync();

                using var host3 = CreateHost<Startup>(3264, config3);
                await host3.StartAsync();

                await listener.Result.WaitAsync(DefaultTimeout);
                Equal(GetLocalClusterView(host1).LocalMemberAddress, listener.Result.Result.EndPoint);

                // add two nodes to the cluster
                True(await GetLocalClusterView(host1).AddMemberAsync(GetLocalClusterView(host2).LocalMemberId, GetLocalClusterView(host2).LocalMemberAddress));
                await GetLocalClusterView(host2).Readiness.WaitAsync(DefaultTimeout);

                True(await GetLocalClusterView(host1).AddMemberAsync(GetLocalClusterView(host3).LocalMemberId, GetLocalClusterView(host3).LocalMemberAddress));
                await GetLocalClusterView(host3).Readiness.WaitAsync(DefaultTimeout);

                Equal(GetLocalClusterView(host1).Leader.EndPoint, GetLocalClusterView(host2).Leader.EndPoint);
                Equal(GetLocalClusterView(host1).Leader.EndPoint, GetLocalClusterView(host3).Leader.EndPoint);

                foreach (var member in GetLocalClusterView(host1).As<IRaftCluster>().Members)
                {
                    if (member.IsRemote)
                    {
                        NotEmpty(await member.GetMetadataAsync());
                    }
                }

                await host3.StopAsync();
                await host2.StopAsync();
                await host1.StopAsync();
            }

            // recover cluster
            config1["coldStart"] = "false";
            listener = new LeaderTracker();
            using (var host1 = CreateHost<Startup>(3262, config1))
            {
                await host1.StartAsync();

                using var host2 = CreateHost<Startup>(3263, config2);
                await host2.StartAsync();

                using var host3 = CreateHost<Startup>(3264, config3);
                await host3.StartAsync();

                var leader1 = GetLocalClusterView(host1).WaitForLeaderAsync(DefaultTimeout);
                var leader2 = GetLocalClusterView(host2).WaitForLeaderAsync(DefaultTimeout);
                var leader3 = GetLocalClusterView(host3).WaitForLeaderAsync(DefaultTimeout);

                foreach (var member in GetLocalClusterView(host1).As<IRaftCluster>().Members)
                {
                    if (member.IsRemote)
                    {
                        NotEmpty(await member.GetMetadataAsync());
                    }
                }

                await host3.StopAsync();
                await host2.StopAsync();
                await host1.StopAsync();
            }
        }

        [Fact]
        public static async Task DependencyInjection()
        {
            var config = new Dictionary<string, string>
            {
                {"metadata:nodeName", "TestNode"},
                {"partitioning", "false"},
                {"publicEndPoint", "http://localhost:3262"},
                {"coldStart", "true"},
            };

            using var host = CreateHost<Startup>(3262, config);
            await host.StartAsync();

            NotNull(host.Services.GetService<ICluster>());
            NotNull(host.Services.GetService<IRaftHttpCluster>());
            NotNull(host.Services.GetService<IRaftCluster>());
            NotNull(host.Services.GetService<IMessageBus>());
            NotNull(host.Services.GetService<IReplicationCluster>());
            NotNull(host.Services.GetService<IReplicationCluster<IRaftLogEntry>>());
            NotNull(host.Services.GetService<IPeerMesh<IRaftClusterMember>>());
            NotNull(host.Services.GetService<IPeerMesh<IClusterMember>>());
            NotNull(host.Services.GetService<IPeerMesh<ISubscriber>>());
            NotNull(host.Services.GetService<IInputChannel>());
            await host.StopAsync();
        }
    }
}