using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster;

/// <summary>
/// Represents arguments of the even raised when the status of the cluster member has changed.
/// </summary>
/// <param name="member">The cluster member associated with the event.</param>
/// <param name="previousStatus">The previous status of the cluster member.</param>
/// <param name="newStatus">The new status of the cluster member.</param>
public class ClusterMemberStatusChangedEventArgs(IClusterMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus) : ClusterMemberEventArgs(member)
{
    /// <summary>
    /// Gets the previous status of the cluster member.
    /// </summary>
    public ClusterMemberStatus PreviousStatus => previousStatus;

    /// <summary>
    /// Gets the new status of the cluster member.
    /// </summary>
    public ClusterMemberStatus NewStatus => newStatus;
}

/// <summary>
/// Represents arguments of the even raised when the status of the cluster member has changed.
/// </summary>
/// <typeparam name="TMember">The type of the member.</typeparam>
/// <param name="member">The cluster member associated with the event.</param>
/// <param name="previousStatus">The previous status of the cluster member.</param>
/// <param name="newStatus">The new status of the cluster member.</param>
public class ClusterMemberStatusChangedEventArgs<TMember>(TMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus) : ClusterMemberStatusChangedEventArgs(member, previousStatus, newStatus)
    where TMember : class, IClusterMember
{
    /// <summary>
    /// Gets a member associated with the event.
    /// </summary>
    public new TMember Member => Unsafe.As<TMember>(base.Member);
}