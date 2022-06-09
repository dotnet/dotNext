using System.Net;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Messaging.Gossip;

[ExcludeFromCodeCoverage]
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
        var manager = new RumorSpreadingManager();
        var endPoint = new IPEndPoint(IPAddress.Loopback, 80);
        var id = new RumorTimestamp();

        True(manager.TryEnableControl(endPoint));
        True(manager.CheckOrder(endPoint, id));
        False(manager.CheckOrder(endPoint, id));
        False(manager.CheckOrder(endPoint, id));

        id = manager.Tick();
        True(manager.CheckOrder(endPoint, id));

        id = manager.Tick();
        True(manager.TryDisableControl(endPoint));
        False(manager.CheckOrder(endPoint, id));
    }
}