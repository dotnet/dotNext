using System;
using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Cluster.Discovery.HyParView.Http
{
    using Buffers;
    using ComponentModel;
    using HttpProtocolVersion = Net.Http.HttpProtocolVersion;

    /// <summary>
    /// Represents configuration of HyParView-over-HTTP implementation.
    /// </summary>
    public class HttpPeerConfiguration : PeerConfiguration
    {
        internal const string DefaultResourcePath = "/membership/hyparview";
        private const string DefaultClientHandlerName = "hyParViewClient";

        static HttpPeerConfiguration()
        {
            PathStringConverter.Register();
        }

        private string? handlerName;

        /// <summary>
        /// Gets or sets resource path of HyParView protocol handler.
        /// </summary>
        [CLSCompliant(false)]
        public PathString ResourcePath { get; set; } = new(DefaultResourcePath);

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
        /// Gets or sets socket connection timeout.
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Gets or sets HTTP handler name used by HyParView peer client.
        /// </summary>
        public string ClientHandlerName
        {
            get => handlerName.IfNullOrEmpty(DefaultClientHandlerName);
            set => handlerName = value;
        }

        /// <summary>
        /// Gets or sets allocator for the internal buffer.
        /// </summary>
        public MemoryAllocator<byte>? Allocator { get; set; }
    }
}