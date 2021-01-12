using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace DotNext.Net.Cluster
{
    using Buffers;

    /// <summary>
    /// Represents unique identifier of cluster member.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public readonly struct ClusterMemberId : IEquatable<ClusterMemberId>, ISerializable
    {
        private const string AddressSerData = "A";
        private const string PortSerData = "P";
        private const string LengthSerData = "L";
        private const string FamilySerData = "F";

        private readonly Guid address;
        private readonly int port, length, family;

        private ClusterMemberId(SerializationInfo info, StreamingContext context)
        {
            address = (Guid)info.GetValue(AddressSerData, typeof(Guid));
            port = info.GetInt32(PortSerData);
            length = info.GetInt32(LengthSerData);
            family = info.GetInt32(FamilySerData);
        }

        internal ClusterMemberId(IPEndPoint endpoint)
        {
            address = default;
            var bytes = Span.AsBytes(ref address);
            if (!endpoint.Address.TryWriteBytes(bytes, out length))
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
            var reader = new SpanReader<byte>(bytes);
            address = reader.Read<Guid>();
            port = reader.Read<int>();
            length = reader.Read<int>();
            family = reader.Read<int>();
        }

        /// <summary>
        /// Determines whether the current identifier is equal
        /// to another identifier.
        /// </summary>
        /// <param name="other">The identifier to compare.</param>
        /// <returns><see langword="true"/> if this identifier is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ClusterMemberId other)
            => address == other.address && port == other.port && length == other.length && family == other.family;

        /// <summary>
        /// Determines whether the current identifier is equal
        /// to another identifier.
        /// </summary>
        /// <param name="other">The identifier to compare.</param>
        /// <returns><see langword="true"/> if this identifier is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ClusterMemberId id && Equals(id);

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
            => Span.AsReadOnlyBytes(this).ToHex();

        /// <summary>
        /// Attempts to parse cluster member identifier.
        /// </summary>
        /// <param name="identifier">The hexadecimal representation of identifier.</param>
        /// <param name="value">The parsed identifier.</param>
        /// <returns><see langword="true"/> if identifier parsed successfully; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(ReadOnlySpan<char> identifier, out ClusterMemberId value)
        {
            value = default;
            var bytes = Span.AsBytes(ref value);
            return identifier.FromHex(bytes) == bytes.Length;
        }

        /// <summary>
        /// Determines whether the two identifiers are equal.
        /// </summary>
        /// <param name="x">The first identifier to compare.</param>
        /// <param name="y">The second identifier to compare.</param>
        /// <returns><see langword="true"/> if both identifiers are equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ClusterMemberId x, in ClusterMemberId y)
            => x.address == y.address && x.port == y.port && x.length == y.length && x.family == y.family;

        /// <summary>
        /// Determines whether the two identifiers are not equal.
        /// </summary>
        /// <param name="x">The first identifier to compare.</param>
        /// <param name="y">The second identifier to compare.</param>
        /// <returns><see langword="true"/> if both identifiers are not equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ClusterMemberId x, in ClusterMemberId y)
            => x.address != y.address || x.port != y.port || x.length != y.length || x.family != y.family;

        /// <inheritdoc/>
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(AddressSerData, address);
            info.AddValue(PortSerData, port);
            info.AddValue(LengthSerData, length);
            info.AddValue(FamilySerData, family);
        }
    }
}