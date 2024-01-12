using System.Net;

namespace DotNext.Net.Cluster;

/// <summary>
/// Represents arguments for all events related to cluster members.
/// </summary>
/// <param name="member">The cluster member.</param>
public class ClusterMemberEventArgs(IClusterMember member) : PeerEventArgs
{
    /// <summary>
    /// Gets a member associated with the event.
    /// </summary>
    public IClusterMember Member => member;

    /// <summary>
    /// Gets the address of the cluster member.
    /// </summary>
    public sealed override EndPoint PeerAddress => member.EndPoint;
}