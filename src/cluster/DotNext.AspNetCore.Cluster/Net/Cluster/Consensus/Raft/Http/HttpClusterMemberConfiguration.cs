using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using HttpProtocolVersion = Net.Http.HttpProtocolVersion;

/// <summary>
/// Represents configuration of Raft HTTP cluster member.
/// </summary>
public class HttpClusterMemberConfiguration : ClusterMemberConfiguration, IClusterMemberConfiguration
{
    private const string DefaultClientHandlerName = "raftClient";

    private string? handlerName;
    private TimeSpan? requestTimeout;

    /// <summary>
    /// Gets or sets the address of the local node visible to the entire cluster.
    /// </summary>
    [CLSCompliant(false)]
    public Uri? PublicEndPoint { get; set; }

    /// <summary>
    /// Gets configuration of request journal.
    /// </summary>
    public RequestJournalConfiguration RequestJournal { get; } = new RequestJournalConfiguration();

    /// <summary>
    /// Specifies that each request should create individual TCP connection (no KeepAlive).
    /// </summary>
    public bool OpenConnectionForEachRequest { get; set; }

    /// <summary>
    /// Gets or sets HTTP version supported by Raft implementation.
    /// </summary>
    public HttpProtocolVersion ProtocolVersion { get; set; }

    /// <summary>
    /// Gets or sets HTTP version policy.
    /// </summary>
    public HttpVersionPolicy ProtocolVersionPolicy { get; set; } = HttpVersionPolicy.RequestVersionOrLower;

    /// <summary>
    /// Gets or sets request timeout used to communicate with cluster members.
    /// </summary>
    /// <value>HTTP request timeout; default is <see cref="ClusterMemberConfiguration.UpperElectionTimeout"/>.</value>
    public TimeSpan RequestTimeout
    {
        get => requestTimeout ?? TimeSpan.FromMilliseconds(UpperElectionTimeout);
        set => requestTimeout = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets HTTP handler name used by Raft node client.
    /// </summary>
    public string ClientHandlerName
    {
        get => handlerName is { Length: > 0 } ? handlerName : DefaultClientHandlerName;
        set => handlerName = value;
    }

    /// <inheritdoc />
    IEqualityComparer<EndPoint> IClusterMemberConfiguration.EndPointComparer => EndPointFormatter.UriEndPointComparer;
}