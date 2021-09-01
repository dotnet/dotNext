using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace DotNext.Net
{
    /// <summary>
    /// Represents HTTP endpoint.
    /// </summary>
    public sealed class HttpEndPoint : DnsEndPoint, ISupplier<UriBuilder>, IEquatable<HttpEndPoint>
    {
        /// <summary>
        /// Initializes a new HTTP endpoint.
        /// </summary>
        /// <param name="uri">The absolute path to Web resource.</param>
        public HttpEndPoint(Uri uri)
            : base(uri.IdnHost, GetPort(uri, out var secure), ToAddressFamily(uri.HostNameType))
        {
            IsSecure = secure;
        }

        /// <summary>
        /// Initializes a new HTTP endpoint.
        /// </summary>
        /// <param name="hostName">The host name or a string representation of the IP address.</param>
        /// <param name="port">The port number associated with the address, or 0 to specify any available port.</param>
        /// <param name="secure"><see langword="true"/> for HTTPS; <see langword="false"/> for HTTP.</param>
        /// <param name="family">The type of the host name.</param>
        public HttpEndPoint(string hostName, int port, bool secure, AddressFamily family = AddressFamily.Unspecified)
            : base(hostName, port, family)
        {
            IsSecure = secure;
        }

        /// <summary>
        /// Initializes a new HTTP endpoint.
        /// </summary>
        /// <param name="address">The address of the endpoint.</param>
        /// <param name="port">The port number associated with the address, or 0 to specify any available port.</param>
        /// <param name="secure"><see langword="true"/> for HTTPS; <see langword="false"/> for HTTP.</param>
        public HttpEndPoint(IPAddress address, int port, bool secure)
            : base(address.ToString(), port, address.AddressFamily)
        {
            IsSecure = secure;
        }

        /// <summary>
        /// Initializes a new HTTP endpoint.
        /// </summary>
        /// <param name="address">The address of the endpoint.</param>
        /// <param name="secure"><see langword="true"/> for HTTPS; <see langword="false"/> for HTTP.</param>
        public HttpEndPoint(IPEndPoint address, bool secure)
            : this(address.Address, address.Port, secure)
        {
        }

        private static int GetPort(Uri uri, out bool secure)
        {
            const int defaultHttpPort = 80;
            const int defaultHttpsPort = 443;

            secure = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            return uri.IsDefaultPort ? secure ? defaultHttpsPort : defaultHttpPort : uri.Port;
        }

        private static AddressFamily ToAddressFamily(UriHostNameType nameType) => nameType switch
        {
            UriHostNameType.IPv4 => AddressFamily.InterNetwork,
            UriHostNameType.IPv6 => AddressFamily.InterNetworkV6,
            _ => AddressFamily.Unspecified,
        };

        /// <summary>
        /// Gets a value indicating that HTTP over TLS should be used (HTTPS).
        /// </summary>
        public bool IsSecure { get; }

        /// <summary>
        /// Gets URI scheme.
        /// </summary>
        public string Scheme => IsSecure ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;

        /// <summary>
        /// Creates a new instance of <see cref="UriBuilder"/> with host, port and scheme imported from this object.
        /// </summary>
        /// <returns>A new instance of <see cref="UriBuilder"/>.</returns>
        public UriBuilder CreateUriBuilder()
            => new UriBuilder(Scheme) { Host = Host, Port = Port };

        /// <inheritdoc />
        UriBuilder ISupplier<UriBuilder>.Invoke() => CreateUriBuilder();

        /// <summary>
        /// Creates a new instance of <see cref="UriBuilder"/> with host, port and scheme imported from this object.
        /// </summary>
        /// <param name="endPoint">The endpoint.</param>
        /// <returns>A new instance of <see cref="UriBuilder"/>.</returns>
        [return: NotNullIfNotNull("endPoint")]
        public static explicit operator UriBuilder?(HttpEndPoint? endPoint) => endPoint?.CreateUriBuilder();

        /// <summary>
        /// Determines whether this object represents the same HTTP endpoint as the specified object.
        /// </summary>
        /// <param name="other">The object to compare.</param>
        /// <returns><see langword="true"/> if this object represents the same endpoint as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public bool Equals(HttpEndPoint? other)
            => other is not null && string.Equals(Host, other.Host, StringComparison.OrdinalIgnoreCase) && Port == other.Port && IsSecure == other.IsSecure && AddressFamily == other.AddressFamily;

        /// <summary>
        /// Determines whether this object represents the same HTTP endpoint as the specified object.
        /// </summary>
        /// <param name="other">The object to compare.</param>
        /// <returns><see langword="true"/> if this object represents the same endpoint as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => Equals(other as HttpEndPoint);

        /// <summary>
        /// Determines whether the objects represent the same HTTP endpoint.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        /// <returns><see langword="true"/> if both objects represent the same endpoint; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(HttpEndPoint? x, HttpEndPoint? y)
            => Equals(x, y);

        /// <summary>
        /// Determines whether the objects represent the different HTTP endpoints.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        /// <returns><see langword="true"/> if both objects represent the different endpoints; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(HttpEndPoint? x, HttpEndPoint? y)
            => !Equals(x, y);

        /// <summary>
        /// Gets a hash code of this instance.
        /// </summary>
        /// <returns>The hash code of this instance.</returns>
        public override int GetHashCode()
        {
            var result = new HashCode();
            result.Add(Host, StringComparer.OrdinalIgnoreCase);
            result.Add(Port);
            result.Add(IsSecure);
            result.Add(AddressFamily);
            return result.ToHashCode();
        }

        /// <summary>
        /// Converts endpoint to its string representation.
        /// </summary>
        /// <returns>The string representation of this end point.</returns>
        public override string ToString() => $"{Scheme}://{Host}:{Port}/";

        /// <summary>
        /// Attempts to parse HTTP endpoint.
        /// </summary>
        /// <param name="str">The string representing HTTP endpoint.</param>
        /// <param name="result">The parsed object; or <see langword="null"/> if parsing failed.</param>
        /// <returns><see langword="true"/> if parsing is successful; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(string? str, [NotNullWhen(true)] out HttpEndPoint? result)
        {
            if (Uri.TryCreate(str, UriKind.Absolute, out var uri))
            {
                result = new(uri);
                return true;
            }

            result = null;
            return false;
        }
    }
}