using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents arguments for all events related to Raft cluster members.
    /// </summary>
    public class RaftClusterMemberEventArgs<TMember> : ClusterMemberEventArgs
        where TMember : class, IRaftClusterMember
    {
        /// <summary>
        /// Initializes a new set of arguments for the event.
        /// </summary>
        /// <param name="member">The type of the member.</param>
        public RaftClusterMemberEventArgs(TMember member)
            : base(member)
        {
        }

        /// <summary>
        /// Gets the member associated with the event.
        /// </summary>
        public new TMember Member => Unsafe.As<TMember>(base.Member);
    }
}