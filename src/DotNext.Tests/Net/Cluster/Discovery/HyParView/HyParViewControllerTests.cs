using System.Net;

namespace DotNext.Net.Cluster.Discovery.HyParView
{
    using HttpPeerConfiguration = Http.HttpPeerConfiguration;

    public sealed class HyParViewControllerTests : Test
    {
        private sealed class TransportLayer : Dictionary<EndPoint, PeerController>
        {

        }

        private sealed class TestPeerController : PeerController
        {
            internal readonly EndPoint Address;
            private readonly EndPoint contactNode;
            private readonly TransportLayer transport;

            internal TestPeerController(HttpPeerConfiguration configuration, TransportLayer transport)
                : base(configuration)
            {
                this.transport = transport;
                Address = configuration.LocalNode;
                contactNode = configuration.ContactNode;
            }

            protected override bool IsLocalNode(EndPoint peer) => Address.Equals(peer);

            public Task StartAsync(CancellationToken token = default)
            {
                transport.Add(Address, this);
                return StartAsync(contactNode, token);
            }

            public override async Task StopAsync(CancellationToken token = default)
            {
                await base.StopAsync(token);
                transport.Remove(Address);
            }

            protected override Task JoinAsync(EndPoint contactNode, CancellationToken token)
                => transport[contactNode].EnqueueJoinAsync(Address, token).AsTask();

            protected override Task NeighborAsync(EndPoint neighbor, bool highPriority, CancellationToken token)
                => transport[neighbor].EnqueueNeighborAsync(Address, highPriority, token).AsTask();

            protected override Task ForwardJoinAsync(EndPoint receiver, EndPoint joinedPeer, int timeToLive, CancellationToken token)
                => transport[receiver].EnqueueForwardJoinAsync(Address, joinedPeer, timeToLive, token).AsTask();

            protected override Task DisconnectAsync(EndPoint peer, bool isAlive, CancellationToken token)
                => transport[peer].EnqueueDisconnectAsync(Address, isAlive, token).AsTask();

            protected override Task ShuffleAsync(EndPoint receiver, EndPoint origin, IReadOnlyCollection<EndPoint> peers, int timeToLive, CancellationToken token)
                => transport[receiver].EnqueueShuffleAsync(Address, origin, peers, timeToLive, token).AsTask();

            protected override Task ShuffleReplyAsync(EndPoint receiver, IReadOnlyCollection<EndPoint> peers, CancellationToken token)
                => transport[receiver].EnqueueShuffleReplyAsync(peers, token).AsTask();
        }

        private static async Task WaitForPeer(PeerController controller, EndPoint peer)
        {
            var listener = new TaskCompletionSource();
            Action<PeerController, PeerEventArgs> handler = (c, a) =>
            {
                if (object.Equals(peer, a.PeerAddress))
                    listener.SetResult();
            };
            controller.PeerDiscovered += handler;
            await listener.Task.WaitAsync(DefaultTimeout);
            controller.PeerDiscovered -= handler;
        }

        [Fact]
        public static async Task ActiveViewOverflow()
        {
            var transport = new TransportLayer();
            using var peer1 = new TestPeerController(new HttpPeerConfiguration
            {
                ActiveViewCapacity = 3,
                PassiveViewCapacity = 6,
                LowerShufflePeriod = 10,
                UpperShufflePeriod = 5,
                LocalNode = new HttpEndPoint("localhost", 3262, false)
            },
            transport);

            await peer1.StartAsync();

            using var peer2 = new TestPeerController(new HttpPeerConfiguration
            {
                ActiveViewCapacity = 3,
                PassiveViewCapacity = 6,
                LowerShufflePeriod = 10,
                UpperShufflePeriod = 5,
                ContactNode = new HttpEndPoint("localhost", 3262, false),
                LocalNode = new HttpEndPoint("localhost", 3263, false)
            },
            transport);

            var task = WaitForPeer(peer2, new HttpEndPoint("localhost", 3262, false));
            await peer2.StartAsync();
            await task.WaitAsync(DefaultTimeout);

            using var peer3 = new TestPeerController(new HttpPeerConfiguration
            {
                ActiveViewCapacity = 3,
                PassiveViewCapacity = 6,
                LowerShufflePeriod = 10,
                UpperShufflePeriod = 5,
                ContactNode = new HttpEndPoint("localhost", 3262, false),
                LocalNode = new HttpEndPoint("localhost", 3264, false)
            },
            transport);

            task = WaitForPeer(peer3, new HttpEndPoint("localhost", 3262, false));
            await peer3.StartAsync();
            await task.WaitAsync(DefaultTimeout);

            using var peer4 = new TestPeerController(new HttpPeerConfiguration
            {
                ActiveViewCapacity = 3,
                PassiveViewCapacity = 6,
                LowerShufflePeriod = 10,
                UpperShufflePeriod = 5,
                ContactNode = new HttpEndPoint("localhost", 3262, false),
                LocalNode = new HttpEndPoint("localhost", 3265, false)
            },
            transport);

            task = WaitForPeer(peer4, new HttpEndPoint("localhost", 3262, false));
            await peer4.StartAsync();
            await task.WaitAsync(DefaultTimeout);

            using var peer5 = new TestPeerController(new HttpPeerConfiguration
            {
                ActiveViewCapacity = 3,
                PassiveViewCapacity = 6,
                LowerShufflePeriod = 10,
                UpperShufflePeriod = 5,
                ContactNode = new HttpEndPoint("localhost", 3262, false),
                LocalNode = new HttpEndPoint("localhost", 3266, false)
            },
            transport);

            task = WaitForPeer(peer5, new HttpEndPoint("localhost", 3262, false));
            await peer5.StartAsync();
            await task.WaitAsync(DefaultTimeout);

            await peer5.StopAsync();
            await peer4.StopAsync();
            await peer3.StopAsync();
            await peer2.StopAsync();
            await peer1.StopAsync();
        }
    }
}