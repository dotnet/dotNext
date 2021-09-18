namespace DotNext.Net.Cluster;

/// <summary>
/// Represents status of cluster member.
/// </summary>
public enum ClusterMemberStatus
{
    /// <summary>
    /// Member status is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Member is unreachable through network (probably, offline).
    /// </summary>
    Unavailable,

    /// <summary>
    /// Member is online.
    /// </summary>
    Available,
}