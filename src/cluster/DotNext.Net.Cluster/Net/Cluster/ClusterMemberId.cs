using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace DotNext.Net.Cluster
{
    using Buffers;
    using Intrinsics = Runtime.Intrinsics;

    /// <summary>
    /// Represents unique identifier of cluster member.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public readonly struct ClusterMemberId : IEquatable<ClusterMemberId>, ISerializable
    {
        /// <summary>
        /// Gets size of this type, in bytes.
        /// </summary>
        public static int Size => 16 + (sizeof(int) * 3);

        private const string AddressSerData = "A";
        private const string PortSerData = "P";
        private const string LengthSerData = "L";
        private const string FamilySerData = "F";
        private static readonly Func<SocketAddress, int, long> SocketAddressByteGetter64 = GetAddressByteAsInt64;
        private static readonly Func<SocketAddress, int, int> SocketAddressByteGetter32 = GetAddressByteAsInt32;

        private readonly Guid address;
        private readonly int port, length, family;

        private ClusterMemberId(SerializationInfo info, StreamingContext context)
        {
            address = (Guid)info.GetValue(AddressSerData, typeof(Guid))!;
            port = info.GetInt32(PortSerData);
            length = info.GetInt32(LengthSerData);
            family = info.GetInt32(FamilySerData);
        }

        private ClusterMemberId(IPEndPoint endpoint)
        {
            address = default;
            var bytes = Span.AsBytes(ref address);
            if (!endpoint.Address.TryWriteBytes(bytes, out length))
                throw new ArgumentException(ExceptionMessages.UnsupportedAddressFamily, nameof(endpoint));
            port = endpoint.Port;
            family = (int)endpoint.AddressFamily;
        }

        private ClusterMemberId(DnsEndPoint endpoint)
        {
            var hostHash = Span.BitwiseHashCode64(endpoint.Host.AsSpan(), false);
            Intrinsics.Bitcast(in hostHash, out address);

            length = endpoint.Host.Length;
            port = endpoint.Port;
            family = (int)endpoint.AddressFamily;
        }

        private ClusterMemberId(SocketAddress address)
        {
            var hash = Intrinsics.GetHashCode64(SocketAddressByteGetter64, address.Size, address, false);
            Intrinsics.Bitcast(in hash, out this.address);

            port = Intrinsics.GetHashCode32(SocketAddressByteGetter32, address.Size, address, false);
            family = (int)address.Family;
            length = address.Size;
        }

        private static long GetAddressByteAsInt64(SocketAddress address, int index)
            => address[index];

        private static int GetAddressByteAsInt32(SocketAddress address, int index)
            => address[index];

        /// <summary>
        /// Initializes a new unique identifier from set of bytes.
        /// </summary>
        /// <param name="bytes">The memory block of bytes.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bytes"/> is too small.</exception>
        public ClusterMemberId(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < Size)
                throw new ArgumentOutOfRangeException(nameof(bytes));

            var reader = new SpanReader<byte>(bytes);
            Parse(ref reader, out address, out port, out length, out family);
        }

        /// <summary>
        /// Initializes a new random unique identifier.
        /// </summary>
        /// <param name="random">The source of random values.</param>
        public ClusterMemberId(Random random)
        {
            Span<byte> bytes = stackalloc byte[16];
            random.NextBytes(bytes);
            address = new(bytes);
            port = random.Next();
            length = random.Next();
            family = random.Next();
        }

        /// <summary>
        /// Deserializes the cluster member ID.
        /// </summary>
        /// <param name="reader">The memory block reader.</param>
        public ClusterMemberId(ref SpanReader<byte> reader)
            => Parse(ref reader, out address, out port, out length, out family);

        private static void Parse(ref SpanReader<byte> reader, out Guid address, out int port, out int length, out int family)
        {
            address = new Guid(reader.Read(16));
            port = reader.ReadInt32(true);
            length = reader.ReadInt32(true);
            family = reader.ReadInt32(true);
        }

        /// <summary>
        /// Serializes the value as a sequence of bytes.
        /// </summary>
        /// <param name="writer">The memory block writer.</param>
        /// <exception cref="System.IO.InternalBufferOverflowException"><paramref name="writer"/> is not large enough.</exception>
        public void WriteTo(ref SpanWriter<byte> writer)
        {
            address.TryWriteBytes(writer.Slide(16));
            writer.WriteInt32(port, true);
            writer.WriteInt32(length, true);
            writer.WriteInt32(family, true);
        }

        /// <summary>
        /// Serializes the value as a sequence of bytes.
        /// </summary>
        /// <param name="output">The output buffer.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="output"/> is not large enough.</exception>
        public void WriteTo(Span<byte> output)
        {
            if (output.Length < Size)
                throw new ArgumentOutOfRangeException(nameof(output));

            var writer = new SpanWriter<byte>(output);
            WriteTo(ref writer);
        }

        /// <summary>
        /// Creates buffered copy of the cluster ID.
        /// </summary>
        /// <param name="allocator">The buffer allocator.</param>
        /// <returns>The buffered copy of the cluster ID.</returns>
        public MemoryOwner<byte> Bufferize(MemoryAllocator<byte>? allocator = null)
        {
            var result = allocator.Invoke(Size, true);
            WriteTo(result.Memory.Span);
            return result;
        }

        /// <summary>
        /// Constructs cluster member id from its endpoint address.
        /// </summary>
        /// <param name="ep">The address of the cluster member.</param>
        /// <returns>The identifier of the cluster member.</returns>
        public static ClusterMemberId FromEndPoint(EndPoint ep) => ep switch
        {
            IPEndPoint ip => new ClusterMemberId(ip),
            DnsEndPoint dns => new ClusterMemberId(dns),
            _ => new ClusterMemberId(ep.Serialize())
        };

        private bool Equals(in ClusterMemberId other)
            => address == other.address && port == other.port && length == other.length && family == other.family;

        /// <summary>
        /// Determines whether the current identifier is equal
        /// to another identifier.
        /// </summary>
        /// <param name="other">The identifier to compare.</param>
        /// <returns><see langword="true"/> if this identifier is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ClusterMemberId other) => Equals(in other);

        /// <summary>
        /// Determines whether the current identifier is equal
        /// to another identifier.
        /// </summary>
        /// <param name="other">The identifier to compare.</param>
        /// <returns><see langword="true"/> if this identifier is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is ClusterMemberId id && Equals(in id);

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
        /// Attempts to parse cluster member identifier.
        /// </summary>
        /// <param name="identifier">The hexadecimal representation of identifier.</param>
        /// <param name="value">The parsed identifier.</param>
        /// <returns><see langword="true"/> if identifier parsed successfully; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(string identifier, out ClusterMemberId value)
            => TryParse(identifier.AsSpan(), out value);

        /// <summary>
        /// Determines whether the two identifiers are equal.
        /// </summary>
        /// <param name="x">The first identifier to compare.</param>
        /// <param name="y">The second identifier to compare.</param>
        /// <returns><see langword="true"/> if both identifiers are equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ClusterMemberId x, in ClusterMemberId y)
            => x.Equals(in y);

        /// <summary>
        /// Determines whether the two identifiers are not equal.
        /// </summary>
        /// <param name="x">The first identifier to compare.</param>
        /// <param name="y">The second identifier to compare.</param>
        /// <returns><see langword="true"/> if both identifiers are not equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ClusterMemberId x, in ClusterMemberId y)
            => !x.Equals(in y);

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