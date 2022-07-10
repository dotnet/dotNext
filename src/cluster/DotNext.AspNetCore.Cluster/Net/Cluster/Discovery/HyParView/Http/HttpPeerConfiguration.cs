namespace DotNext.Net.Cluster.Discovery.HyParView.Http;

using Buffers;
using HttpProtocolVersion = Net.Http.HttpProtocolVersion;

/// <summary>
/// Represents configuration of HyParView-over-HTTP implementation.
/// </summary>
public class HttpPeerConfiguration : PeerConfiguration
{
    private const string DefaultClientHandlerName = "HyParViewClient";

    private string? handlerName;
    private Uri? contactNode, localNode;

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
        get => handlerName is { Length: > 0 } ? handlerName : DefaultClientHandlerName;
        set => handlerName = value;
    }

    /// <summary>
    /// Gets or sets allocator for the internal buffer.
    /// </summary>
    public MemoryAllocator<byte>? Allocator { get; set; }

    /// <summary>
    /// Gets or sets the address of the contact node.
    /// </summary>
    public Uri? ContactNode
    {
        get => contactNode;
        set
        {
            if (value is { IsAbsoluteUri: false })
                throw new ArgumentException(ExceptionMessages.AbsoluteUriExpected(value), nameof(value));

            contactNode = value;
        }
    }

    /// <summary>
    /// Gets or sets the address of the local node.
    /// </summary>
    public Uri? LocalNode
    {
        get => localNode;
        set
        {
            if (value is { IsAbsoluteUri: false })
                throw new ArgumentException(ExceptionMessages.AbsoluteUriExpected(value), nameof(value));

            localNode = value;
        }
    }
}