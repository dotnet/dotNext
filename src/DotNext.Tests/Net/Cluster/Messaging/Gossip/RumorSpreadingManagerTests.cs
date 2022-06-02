using System.Net;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Messaging.Gossip
{
    [ExcludeFromCodeCoverage]
    public sealed class RumorSpreadingManagerTests : Test
    {
        [Fact]
        public static void MissingEndPoint()
        {
            var manager = new RumorSpreadingManager();
            False(manager.CheckMessageOrder(new IPEndPoint(IPAddress.Loopback, 80), default, 10L));
        }

        [Fact]
        public static void IncorrectOrder()
        {
            var manager = new RumorSpreadingManager();
            var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
            var id = new PeerTransientId(Random.Shared);
            Thread.Sleep(30);
            var id2 = new PeerTransientId(Random.Shared);

            True(manager.TryEnableMessageOrderControl(endPoint));
            var timestamp = manager.Tick();

            True(manager.CheckMessageOrder(endPoint, id, timestamp));
            False(manager.CheckMessageOrder(endPoint, id, timestamp));
            False(manager.CheckMessageOrder(endPoint, id, timestamp - 1));

            timestamp = manager.Tick();
            True(manager.CheckMessageOrder(endPoint, id2, timestamp));

            timestamp = manager.Tick();
            False(manager.CheckMessageOrder(endPoint, id, timestamp));
        }
    }
}