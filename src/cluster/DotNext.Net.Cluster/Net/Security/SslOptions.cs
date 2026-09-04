using System.Net;
using System.Net.Security;

namespace DotNext.Net.Security;

/// <summary>
/// Represents transport-level encryption options.
/// </summary>
public class SslOptions
{
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
        get => field ??= new();
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets client-side options.
    /// </summary>
    public SslClientAuthenticationOptions ClientOptions
    {
        get => field ??= new();
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets client-side options for the specified server address.
    /// </summary>
    /// <param name="serverAddress">The address of the server.</param>
    /// <returns>The client-side options.</returns>
    public virtual SslClientAuthenticationOptions CreateClientOptions(EndPoint serverAddress)
        => ClientOptions;
}