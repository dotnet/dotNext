using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents arguments for all events related to Raft cluster members.
/// </summary>
/// <typeparam name="TMember">The type of the cluster member.</typeparam>
/// <param name="member">The type of the member.</param>
public class RaftClusterMemberEventArgs<TMember>(TMember member) : ClusterMemberEventArgs(member)
    where TMember : class, IRaftClusterMember
{
    /// <summary>
    /// Gets the member associated with the event.
    /// </summary>
    public new TMember Member => Unsafe.As<TMember>(base.Member);
}