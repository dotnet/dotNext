using DotNext.Net;
using DotNext.Net.Cluster.Discovery.HyParView;

namespace HyParViewPeer;

internal sealed class HyParViewPeerLifetime : IPeerLifetime
{
    private static void OnPeerDiscovered(PeerController controller, PeerEventArgs args)
    {
        Console.WriteLine($"Peer {args.PeerAddress} has been discovered by the local node");
    }

    private static void OnPeerGone(PeerController controller, PeerEventArgs args)
    {
        Console.WriteLine($"Peer {args.PeerAddress} has been removed from the mesh");
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