using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Discovery.HyParView
{
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

        /// <summary>
        /// Attempts to provide the address of the contact node asynchronously.
        /// </summary>
        /// <remarks>
        /// This method is used to resolve the contact node
        /// if <see cref="PeerConfiguration.ContactNode"/> is not provided.
        /// </remarks>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The address of the contact node; or <see langword="null"/> if the address cannot be provided.</returns>
        ValueTask<EndPoint?> TryResolveContactNodeAsync(CancellationToken token = default) => new(default(EndPoint));

        /// <summary>
        /// Attempts to provide the address of the local node asynchronously.
        /// </summary>
        /// <remarks>
        /// This method is used to resolve the contact node
        /// if <see cref="PeerConfiguration.LocalNode"/> is not provided.
        /// </remarks>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The address of the local node; or <see langword="null"/> if the address cannot be provided.</returns>
        ValueTask<EndPoint?> TryResolveLocalNodeAsync(CancellationToken token = default) => new(default(EndPoint));
    }
}