using System.Net.Security;

namespace DotNext.Net.Security
{
    /// <summary>
    /// Represents transport-level encryption options.
    /// </summary>
    public class SslOptions
    {
        /// <summary>
        /// Gets server-side options.
        /// </summary>
        public SslServerAuthenticationOptions ServerOptions { get; } = new SslServerAuthenticationOptions();

        /// <summary>
        /// Gets client-side options.
        /// </summary>
        public SslClientAuthenticationOptions ClientOptions { get; } = new SslClientAuthenticationOptions();
    }
}