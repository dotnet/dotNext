using System;
using System.Net;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster
{
    using Intrinsics = Runtime.Intrinsics;

    /// <summary>
    /// Represents unique identifier of cluster member.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ClusterMemberId : IEquatable<ClusterMemberId>
    {
        private readonly Guid address;
        private readonly int port, length, family;

        internal ClusterMemberId(IPEndPoint endpoint)
        {
            address = default;
            var bytes = Intrinsics.AsSpan(ref address);
            if(!endpoint.Address.TryWriteBytes(bytes, out length))
                throw new ArgumentException(ExceptionMessages.UnsupportedAddressFamily, nameof(endpoint));
            port = endpoint.Port;
            family = (int)endpoint.AddressFamily;
        }

        /// <summary>
        /// Initializes a new unique identifier from set of bytes.
        /// </summary>
        /// <param name="bytes">The memory block of bytes.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bytes"/> is too small.</exception>
        public ClusterMemberId(ReadOnlySpan<byte> bytes)
        {
            address = Span.Read<Guid>(ref bytes);
            port = Span.Read<int>(ref bytes);
            length = Span.Read<int>(ref bytes);
            family = Span.Read<int>(ref bytes);
        }

        public bool Equals(ClusterMemberId other)
            => address == other.address && port == other.port && length == other.length && family == other.family;
        
        public override bool Equals(object other) => other is ClusterMemberId id && Equals(id);

        /// <summary>
        /// Gets the hash code of this identifier.
        /// </summary>
        /// <returns>The hash code of this identifier.</returns>
        public override int GetHashCode() => HashCode.Combine(address, port, length, family);

        /// <summary>
        /// Returns hexadecimal representation of this identifier.
        /// </summary>
        /// <returns>The hexadecimal representation of this identifier.</returns>
        public override string ToString()
            => Intrinsics.AsReadOnlySpan(this).ToHex();

        public static bool operator ==(in ClusterMemberId x, in ClusterMemberId y)
            => x.address == y.address && x.port == y.port && x.length == y.length && x.family == y.family;
    
        public static bool operator !=(in ClusterMemberId x, in ClusterMemberId y)
            => x.address != y.address || x.port != y.port || x.length != y.length || x.family != y.family;
    }
}