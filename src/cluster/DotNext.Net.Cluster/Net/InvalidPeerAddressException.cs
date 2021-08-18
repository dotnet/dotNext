using System;
using System.Net;

namespace DotNext.Net
{
    /// <summary>
    /// Indicates that the peer with the given address is not reachable from the current peer.
    /// </summary>
    public sealed class InvalidPeerAddressException : ArgumentException
    {
        /// <summary>
        /// Initializes a new exception.
        /// </summary>
        /// <param name="paramName">The name of the parameter representing the peer.</param>
        /// <param name="peer">The actual value of the parameter representing the peer.</param>
        public InvalidPeerAddressException(string paramName, EndPoint peer)
            : base(ExceptionMessages.InvalidPeerAddress(peer), paramName)
        {
            Peer = peer;
        }

        /// <summary>
        /// Gets unreachable peer.
        /// </summary>
        public EndPoint Peer { get; }
    }
}