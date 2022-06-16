using DotNext.Net;
using DotNext.Net.Cluster.Discovery.HyParView;
using DotNext.Net.Cluster.Messaging.Gossip;

namespace HyParViewPeer;

internal sealed class HyParViewPeerLifetime : IPeerLifetime
{
    private readonly RumorSpreadingManager spreadingManager;

    public HyParViewPeerLifetime(RumorSpreadingManager spreadingManager)
        => this.spreadingManager = spreadingManager;

    private void OnPeerDiscovered(PeerController controller, PeerEventArgs args)
    {
        Console.WriteLine($"Peer {args.PeerAddress} has been discovered by the current node");
        spreadingManager.TryEnableControl(args.PeerAddress);
    }

    private void OnPeerGone(PeerController controller, PeerEventArgs args)
    {
        Console.WriteLine($"Peer {args.PeerAddress} is no longer visible by the current node");
        spreadingManager.TryDisableControl(args.PeerAddress);
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