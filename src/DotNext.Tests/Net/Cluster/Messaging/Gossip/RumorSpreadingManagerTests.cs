using System.Net;

namespace DotNext.Net.Cluster.Messaging.Gossip;

public sealed class RumorSpreadingManagerTests : Test
{
    [Fact]
    public static void MissingEndPoint()
    {
        var manager = new RumorSpreadingManager();
        False(manager.CheckOrder(new IPEndPoint(IPAddress.Loopback, 80), default));
    }

    [Fact]
    public static void MessageOrder()
    {
        var sender = new RumorSpreadingManager();
        var receiver = new RumorSpreadingManager();
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
        var id = sender.Tick();

        True(receiver.TryEnableControl(endPoint));
        True(receiver.CheckOrder(endPoint, id));
        False(receiver.CheckOrder(endPoint, id));
        False(receiver.CheckOrder(endPoint, id));

        id = sender.Tick();
        True(receiver.CheckOrder(endPoint, id));

        id = sender.Tick();
        True(receiver.TryDisableControl(endPoint));
        False(receiver.CheckOrder(endPoint, id));
    }
}