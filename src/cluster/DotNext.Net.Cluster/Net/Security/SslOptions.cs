using System.Net.Security;

namespace DotNext.Net.Security;

/// <summary>
/// Represents transport-level encryption options.
/// </summary>
public class SslOptions
{
    private SslServerAuthenticationOptions? serverOptions;
    private SslClientAuthenticationOptions? clientOptions;

    /// <summary>
    /// Initializes a new SSL options with preconfigured options for server and client.
    /// </summary>
    /// <param name="serverOptions">Server-side SSL options.</param>
    /// <param name="clientOptions">Client-side SSL options.</param>
    /// <exception cref="ArgumentNullException"><paramref name="clientOptions"/> or <paramref name="serverOptions"/> is <see langword="null"/>.</exception>
    [Obsolete("Use init-only properties to initialize the instance")]
    public SslOptions(SslServerAuthenticationOptions serverOptions, SslClientAuthenticationOptions clientOptions)
    {
        this.serverOptions = serverOptions ?? throw new ArgumentNullException(nameof(serverOptions));
        this.clientOptions = clientOptions ?? throw new ArgumentNullException(nameof(clientOptions));
    }

    /// <summary>
    /// Initializes empty SSL options for server and client.
    /// </summary>
    public SslOptions()
    {
    }

    /// <summary>
    /// Gets server-side options.
    /// </summary>
    public SslServerAuthenticationOptions ServerOptions
    {
        get => serverOptions ??= new();
        init => serverOptions = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets client-side options.
    /// </summary>
    public SslClientAuthenticationOptions ClientOptions
    {
        get => clientOptions ??= new();
        init => clientOptions = value ?? throw new ArgumentNullException(nameof(value));
    }
}