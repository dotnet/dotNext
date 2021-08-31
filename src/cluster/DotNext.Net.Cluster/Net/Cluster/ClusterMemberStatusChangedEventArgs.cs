using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster
{
    /// <summary>
    /// Represents arguments of the even raised when the status of the cluster member has changed.
    /// </summary>
    public class ClusterMemberStatusChangedEventArgs : ClusterMemberEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the event.
        /// </summary>
        /// <param name="member">The cluster member associated with the event.</param>
        /// <param name="previousStatus">The previous status of the cluster member.</param>
        /// <param name="newStatus">The new status of the cluster member.</param>
        public ClusterMemberStatusChangedEventArgs(IClusterMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus)
            : base(member)
        {
            PreviousStatus = previousStatus;
            NewStatus = newStatus;
        }

        /// <summary>
        /// Gets the previous status of the cluster member.
        /// </summary>
        public ClusterMemberStatus PreviousStatus { get; }

        /// <summary>
        /// Gets the new status of the cluster member.
        /// </summary>
        public ClusterMemberStatus NewStatus { get; }
    }

    /// <summary>
    /// Represents arguments of the even raised when the status of the cluster member has changed.
    /// </summary>
    /// <typeparam name="TMember">The type of the member.</typeparam>
    public class ClusterMemberStatusChangedEventArgs<TMember> : ClusterMemberStatusChangedEventArgs
        where TMember : class, IClusterMember
    {
        /// <summary>
        /// Initializes a new instance of the event.
        /// </summary>
        /// <param name="member">The cluster member associated with the event.</param>
        /// <param name="previousStatus">The previous status of the cluster member.</param>
        /// <param name="newStatus">The new status of the cluster member.</param>
        public ClusterMemberStatusChangedEventArgs(TMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus)
            : base(member, previousStatus, newStatus)
        {
        }

        /// <summary>
        /// Gets a member associated with the event.
        /// </summary>
        public new TMember Member => Unsafe.As<TMember>(base.Member);
    }
}