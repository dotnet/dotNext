using System;
using System.Net;
using System.Runtime.Serialization;

namespace DotNext.Net.Cluster.Discovery
{
    /// <summary>
    /// Represents violation of peer discovery protocol.
    /// </summary>
    [Serializable]
    public abstract class DiscoveryProtocolException : ProtocolViolationException
    {
        /// <summary>
        /// Initializes a new exception.
        /// </summary>
        /// <param name="message">Human-readable text describing problem.</param>
        protected DiscoveryProtocolException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="info">The serialized information about object.</param>
        /// <param name="context">The deserialization context.</param>
        protected DiscoveryProtocolException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}