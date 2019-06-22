using System;
using System.Net;

namespace DotNext
{
    /// <summary>
    /// Represents various extensions for <see cref="UriBuilder"/>.
    /// </summary>
    public static class UriBuilderExtensions
    {
        /// <summary>
        /// Changes host and port according with provided network endpoint.
        /// </summary>
        /// <param name="builder">The builder to be changed.</param>
        /// <param name="endpoint">The network endpoint.</param>
        /// <returns>The modified builder.</returns>
        public static UriBuilder SetHostAndPort(this UriBuilder builder, IPEndPoint endpoint)
        {
            builder.Port = endpoint.Port;
            builder.Host = endpoint.Address.ToString();
            return builder;
        }
    }
}