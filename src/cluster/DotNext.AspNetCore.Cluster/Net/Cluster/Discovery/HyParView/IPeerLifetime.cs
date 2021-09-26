namespace DotNext.Net.Cluster.Discovery.HyParView;

/// <summary>
/// Allows to capture peer lifecycle events.
/// </summary>
/// <remarks>
/// The service of this type must be registered in DI to be associated
/// with <see cref="PeerController"/> lifecycle.
/// </remarks>
public interface IPeerLifetime
{
    /// <summary>
    /// Called automatically when the peer controller has started.
    /// </summary>
    /// <param name="controller">The started peer controller.</param>
    void OnStart(PeerController controller);

    /// <summary>
    /// Called automatically when the peer controller is about to be stopped.
    /// </summary>
    /// <param name="controller">The stopped peer controller.</param>
    void OnStop(PeerController controller);
}