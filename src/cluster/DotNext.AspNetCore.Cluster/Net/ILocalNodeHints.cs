using System.Net;

namespace DotNext.Net
{
    internal interface ILocalNodeHints
    {
        /// <summary>
        /// Gets the address of the local node.
        /// </summary>
        IPAddress? HostAddressHint { get; }

        /// <summary>
        /// Gets DNS name of the local node visible to other nodes in the network.
        /// </summary>
        string? HostNameHint { get; }
    }
}