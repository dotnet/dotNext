namespace DotNext.Net.Cluster.Consensus.Raft.Extensions;

using IFailureDetector = Diagnostics.IFailureDetector;

/// <summary>
/// Provides support of automatic detection and removal of unresponsive cluster members.
/// </summary>
public interface IUnresponsiveClusterMemberRemovalSupport : IRaftCluster
{
    /// <summary>
    /// Sets failure detector to be used by the leader node to detect and remove unresponsive followers.
    /// </summary>
    Func<TimeSpan, IRaftClusterMember, IFailureDetector>? FailureDetectorFactory { init; }
}