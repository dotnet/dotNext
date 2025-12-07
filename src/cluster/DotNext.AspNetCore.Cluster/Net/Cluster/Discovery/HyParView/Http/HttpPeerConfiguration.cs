using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace DotNext.Net.Cluster.Discovery.HyParView.Http;

using HttpProtocolVersion = Net.Http.HttpProtocolVersion;

/// <summary>
/// Represents configuration of HyParView-over-HTTP implementation.
/// </summary>
public class HttpPeerConfiguration : PeerConfiguration, IPeerConfiguration
{
    private const string DefaultClientHandlerName = "HyParViewClient";

    /// <summary>
    /// Gets or sets HTTP version supported by HyParView implementation.
    /// </summary>
    public HttpProtocolVersion ProtocolVersion { get; set; }

    /// <summary>
    /// Gets or sets HTTP protocol selection policy.
    /// </summary>
    public HttpVersionPolicy ProtocolVersionPolicy { get; set; } = HttpVersionPolicy.RequestVersionOrLower;

    /// <summary>
    /// Gets or sets HTTP request timeout.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets HTTP handler name used by HyParView peer client.
    /// </summary>
    public string ClientHandlerName
    {
        get => field is { Length: > 0 } ? field : DefaultClientHandlerName;
        set;
    }

    /// <summary>
    /// Gets or sets the address of the contact node.
    /// </summary>
    public Uri? ContactNode
    {
        get;
        set
        {
            if (value is { IsAbsoluteUri: false })
                throw new ArgumentException(ExceptionMessages.AbsoluteUriExpected(value), nameof(value));

            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the address of the local node.
    /// </summary>
    [DisallowNull]
    public Uri? LocalNode
    {
        get;
        set
        {
            if (value is { IsAbsoluteUri: false })
                throw new ArgumentException(ExceptionMessages.AbsoluteUriExpected(value), nameof(value));

            field = value;
        }
    }

    /// <inheritdoc />
    IEqualityComparer<EndPoint> IPeerConfiguration.EndPointComparer => EndPointFormatter.UriEndPointComparer;
}