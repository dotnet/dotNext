using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging.Gossip
{
    /// <summary>
    /// Represents broadcast command.
    /// </summary>
    public interface IRumourSender : IAsyncDisposable
    {
        /// <summary>
        /// Sends custom message to the specified peer from the active view.
        /// </summary>
        /// <param name="peer">The peer from active view.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        Task SendAsync(EndPoint peer, CancellationToken token);
    }
}