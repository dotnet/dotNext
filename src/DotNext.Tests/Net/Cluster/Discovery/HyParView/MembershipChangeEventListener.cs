using System.Net;

namespace DotNext.Net.Cluster.Discovery.HyParView;

internal sealed class MembershipChangeEventListener : IPeerLifetime
{
    private readonly TaskCompletionSource<EndPoint> discovered, gone;

    internal MembershipChangeEventListener()
    {
        discovered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        gone = new(TaskCreationOptions.RunContinuationsAsynchronously);
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

    private void OnPeerDiscovered(PeerController sender, PeerEventArgs args)
        => discovered.TrySetResult(args.PeerAddress);

    private void OnPeerGone(PeerController sender, PeerEventArgs args)
        => gone.TrySetResult(args.PeerAddress);

    internal Task<EndPoint> DiscoveryTask => discovered.Task;

    internal Task<EndPoint> DisconnectionTask => gone.Task;
}