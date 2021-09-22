using DotNext.Net;
using DotNext.Net.Cluster.Discovery.HyParView;

namespace HyParViewPeer;

internal sealed class HyParViewPeerLifetime : IPeerLifetime
{
    private static void OnPeerDiscovered(PeerController controller, PeerEventArgs args)
    {
        Console.WriteLine($"Peer {args.PeerAddress} has been discovered by the current node");
    }

    private static void OnPeerGone(PeerController controller, PeerEventArgs args)
    {
        Console.WriteLine($"Peer {args.PeerAddress} is no longer visible by the current node");
    }

    void IPeerLifetime.OnStart(PeerController controller)
    {
        controller.PeerDiscovered += OnPeerDiscovered;
        controller.PeerGone += OnPeerGone;
    }

    void IPeerLifetime.OnStop(PeerController controller)
    {
        controller.PeerDiscovered -= OnPeerDiscovered;
        controller.PeerGone -= OnPeerGone;
    }
}