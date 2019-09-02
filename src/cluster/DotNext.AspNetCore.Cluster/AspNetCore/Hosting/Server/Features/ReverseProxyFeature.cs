namespace DotNext.AspNetCore.Hosting.Server.Features
{
    /// <summary>
    /// Indicates that the request is passed through the proxy HTTP server.
    /// </summary>
    /// <remarks>
    /// This feature can be added to the collection of <see cref="Microsoft.AspNetCore.Http.HttpContext.Features"/>.
    /// </remarks>
    public sealed class ReverseProxyFeature
    {
        /// <summary>
        /// Initializes a new instance of the feature.
        /// </summary>
        /// <param name="value"><see langword="true"/> indicating that the request is passed throught the proxy; otherwise, <see langword="false"/>.</param>
        public ReverseProxyFeature(bool value) => IsProxied = value;

        /// <summary>
        /// Gets value indicating that the request is passed through proxy server.
        /// </summary>
        public bool IsProxied { get; }
    }
}
