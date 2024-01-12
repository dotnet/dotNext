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