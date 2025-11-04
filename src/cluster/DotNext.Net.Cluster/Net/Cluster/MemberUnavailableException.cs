namespace DotNext.Net.Cluster;

/// <summary>
/// Indicates that remote member cannot be replicated because it is unreachable through the network.
/// </summary>
/// <param name="member">The unavailable member.</param>
/// <param name="message">Human-readable text describing the issue.</param>
/// <param name="innerException">The underlying network-related exception.</param>
public class MemberUnavailableException(IClusterMember member, string? message = null, Exception? innerException = null)
    : IOException(message ?? ExceptionMessages.UnavailableMember, innerException)
{
    /// <summary>
    /// Gets unavailable member.
    /// </summary>
    public IClusterMember Member => member;
}