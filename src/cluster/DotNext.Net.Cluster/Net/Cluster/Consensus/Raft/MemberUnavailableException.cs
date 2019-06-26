using System;
using System.Runtime.Serialization;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Replication;

    /// <summary>
    /// Indicates that remote member cannot be replicated because it is unreachable through the network.
    /// </summary>
    [Serializable]
    public class MemberUnavailableException : ReplicationException
    {
        private const string InnerExceptionSerEntry = "InnerException";

        private readonly Exception innerException;

        /// <summary>
        /// Initializes a new instance of exception.
        /// </summary>
        /// <param name="member">The unavailable member.</param>
        /// <param name="message">Human-readable text describing the issue.</param>
        public MemberUnavailableException(IRaftClusterMember member, string message)
            : this(member, message, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of exception.
        /// </summary>
        /// <param name="member">The unavailable member.</param>
        /// <param name="message">Human-readable text describing the issue.</param>
        /// <param name="innerException">The underlying network-related exception.</param>
        public MemberUnavailableException(IRaftClusterMember member, string message, Exception innerException)
            : base(member, message)
            => this.innerException = innerException;

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="info">The serialized information about object.</param>
        /// <param name="context">The deserialization context.</param>
        protected MemberUnavailableException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            innerException = (Exception)info.GetValue(InnerExceptionSerEntry, typeof(Exception));
        }

        /// <summary>
        /// Gets the underlying network-related exception.
        /// </summary>
        public sealed override Exception GetBaseException() => innerException ?? base.GetBaseException();

        /// <summary>
        /// Serializes this exception.
        /// </summary>
        /// <param name="info">The serialized information about object.</param>
        /// <param name="context">The serialization context.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(InnerExceptionSerEntry, innerException, innerException?.GetType() ?? typeof(Exception));
        }
    }
}
