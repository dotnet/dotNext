namespace DotNext.Hosting.Server.Features
{
    /// <summary>
    /// Represents simple feature that can be used to inform HyParView, SWIM
    /// and other implementations about TLS mode of HTTP server.
    /// </summary>
    /// <seealso cref="Microsoft.AspNetCore.Hosting.Server.IServer.Features"/>
    public sealed class TransportLayerSecurityFeature
    {
        /// <summary>
        /// Initializes a new feature.
        /// </summary>
        /// <param name="enabled">
        /// <see langword="true"/> if TLS is enabled;
        /// <see langword="false"/> if TLS is disabled.
        /// </param>
        public TransportLayerSecurityFeature(bool enabled)
            => IsEnabled = enabled;

        /// <summary>
        /// Gets a value indicating whether Transport-Layer Security is enabled or not.
        /// </summary>
        public bool IsEnabled { get; }
    }
}