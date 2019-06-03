using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents cluster member that supports messaging.
    /// </summary>
    public interface IMessenger : IClusterMember
    {
        /// <summary>
        /// Sends a message to the cluster member.
        /// </summary>
        /// <param name="request">The message representing request.</param>
        /// <param name="oneWay"><see langword="true"/> to send a request without waiting for the response; <see langword="false"/> to send a request and wait for a response.</param>
        /// <returns>The message representing response; or <see langword="null"/> if request message in one-way.</returns>
        Task<IMessage> SendMessageAsync(IMessage request, bool oneWay = false);
    }
}