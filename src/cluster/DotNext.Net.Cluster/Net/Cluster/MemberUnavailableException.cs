namespace DotNext.Net.Cluster;

/// <summary>
/// Indicates that remote member cannot be replicated because it is unreachable through the network.
/// </summary>
public class MemberUnavailableException : IOException
{
    /// <summary>
    /// Initializes a new instance of exception.
    /// </summary>
    /// <param name="member">The unavailable member.</param>
    /// <param name="message">Human-readable text describing the issue.</param>
    /// <param name="innerException">The underlying network-related exception.</param>
    public MemberUnavailableException(IClusterMember member, string message, Exception? innerException = null)
        : base(message, innerException) => Member = member;

    /// <summary>
    /// Gets unavailable member.
    /// </summary>
    public IClusterMember Member { get; }
}