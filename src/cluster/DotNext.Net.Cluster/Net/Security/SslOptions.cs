using System.Net.Security;

namespace DotNext.Net.Security;

/// <summary>
/// Represents transport-level encryption options.
/// </summary>
public class SslOptions
{
    /// <summary>
    /// Initializes a new SSL options with preconfigured options for server and client.
    /// </summary>
    /// <param name="serverOptions">Server-side SSL options.</param>
    /// <param name="clientOptions">Client-side SSL options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="clientOptions"/> or <paramref name="serverOptions"/> is <see langword="null"/>.</exception>
    public SslOptions(SslServerAuthenticationOptions serverOptions, SslClientAuthenticationOptions clientOptions)
    {
        ServerOptions = serverOptions ?? throw new ArgumentNullException(nameof(serverOptions));
        ClientOptions = clientOptions ?? throw new ArgumentNullException(nameof(clientOptions));
    }

    /// <summary>
    /// Initializes empty SSL options for server and client.
    /// </summary>
    public SslOptions()
        : this(new SslServerAuthenticationOptions(), new SslClientAuthenticationOptions())
    {
    }

    /// <summary>
    /// Gets server-side options.
    /// </summary>
    public SslServerAuthenticationOptions ServerOptions { get; }

    /// <summary>
    /// Gets client-side options.
    /// </summary>
    public SslClientAuthenticationOptions ClientOptions { get; }
}