using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Connections;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster;

using Buffers;
using IO.Hashing;
using Hex = Buffers.Text.Hex;
using HttpEndPoint = Http.HttpEndPoint;

/// <summary>
/// Represents unique identifier of cluster member.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct ClusterMemberId : IEquatable<ClusterMemberId>, IBinaryFormattable<ClusterMemberId>
{
    /// <summary>
    /// Gets size of this type, in bytes.
    /// </summary>
    public static unsafe int Size => sizeof(Guid) + sizeof(ulong) + sizeof(int);

    private readonly Guid address;
    private readonly ulong lengthAndPort; // pack two fields as 8 bytes for more efficient equality operation
    private readonly int family;

    private ClusterMemberId(IPEndPoint endPoint)
    {
        Span<byte> bytes = stackalloc byte[16];
        address = endPoint.Address.TryWriteBytes(bytes, out var length)
            ? new(bytes)
            : throw new ArgumentException(ExceptionMessages.UnsupportedAddressFamily, nameof(endPoint));
        lengthAndPort = Combine(length, endPoint.Port);
        family = (int)endPoint.AddressFamily;
    }

    private ClusterMemberId(DnsEndPoint endPoint)
    {
        Span<byte> bytes = stackalloc byte[16];
        WriteInt64LittleEndian(bytes, FNV1a64.Hash<char>(endPoint.Host));
        address = new(bytes);

        lengthAndPort = Combine(endPoint.Host.Length, endPoint.Port);
        family = (int)endPoint.AddressFamily;
    }

    private ClusterMemberId(HttpEndPoint endPoint)
    {
        var writer = new SpanWriter<byte>(stackalloc byte[16]);
        writer.WriteLittleEndian(FNV1a64.Hash<char>(endPoint.Host));
        writer.Add(endPoint.IsSecure.ToByte());
        address = new(writer.Span);

        lengthAndPort = Combine(endPoint.Host.Length, endPoint.Port);
        family = (int)endPoint.AddressFamily;
    }

    private ClusterMemberId(Uri uri)
    {
        var writer = new SpanWriter<byte>(stackalloc byte[16]);
        writer.WriteLittleEndian(FNV1a32.Hash<char>(uri.Scheme));
        writer.WriteLittleEndian(FNV1a32.Hash<char>(uri.Host));
        writer.WriteLittleEndian(FNV1a64.Hash<char>(uri.PathAndQuery));
        address = new(writer.Span);

        lengthAndPort = Combine(uri.AbsoluteUri.Length, uri.Port);
        family = (int)uri.HostNameType;
    }

    private ClusterMemberId(SocketAddress address)
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes.Clear();
        WriteInt64LittleEndian(bytes, FNV1a64.Hash(static (address, index) => address[index], address.Size, address));
        this.address = new(bytes);

        lengthAndPort = unchecked((uint)address.Size);
        family = (int)address.Family;
    }

    private static ulong Combine(int length, int port)
        => unchecked((uint)length | (((ulong)port) << 32));

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
        this = new(ref reader);
    }

    /// <summary>
    /// Initializes a new random unique identifier.
    /// </summary>
    /// <param name="random">The source of random values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> is <see langword="null"/>.</exception>
    public ClusterMemberId(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        Span<byte> bytes = stackalloc byte[16];
        random.NextBytes(bytes);
        address = new(bytes);
        lengthAndPort = random.Next<ulong>();
        family = random.Next();
    }

    /// <summary>
    /// Deserializes the cluster member ID.
    /// </summary>
    /// <param name="reader">The memory block reader.</param>
    public ClusterMemberId(ref SpanReader<byte> reader)
    {
        address = new(reader.Read(16));
        lengthAndPort = reader.ReadLittleEndian<ulong>(isUnsigned: true);
        family = reader.ReadLittleEndian<int>(isUnsigned: false);
    }

    /// <inheritdoc cref="IBinaryFormattable{T}.Parse(ref SpanReader{byte})"/>
    static ClusterMemberId IBinaryFormattable<ClusterMemberId>.Parse(ref SpanReader<byte> input)
        => new(ref input);

    /// <summary>
    /// Serializes the value as a sequence of bytes.
    /// </summary>
    /// <param name="writer">The memory block writer.</param>
    /// <exception cref="InternalBufferOverflowException"><paramref name="writer"/> is not large enough.</exception>
    public void Format(ref SpanWriter<byte> writer)
    {
        address.TryWriteBytes(writer.Slide(16));
        writer.WriteLittleEndian(lengthAndPort);
        writer.WriteLittleEndian(family);
    }

    /// <summary>
    /// Constructs cluster member id from its endpoint address.
    /// </summary>
    /// <param name="ep">The address of the cluster member.</param>
    /// <returns>The identifier of the cluster member.</returns>
    public static ClusterMemberId FromEndPoint(EndPoint? ep) => ep switch
    {
        null => default,
        IPEndPoint ip => new(ip),
        HttpEndPoint http => new(http),
        DnsEndPoint dns => new(dns),
        UriEndPoint uri => new(uri.Uri),
        _ => new(ep.Serialize())
    };

    private bool Equals(in ClusterMemberId other) => address.Equals(other.address)
            && lengthAndPort == other.lengthAndPort
            && family == other.family;

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
    public override bool Equals([NotNullWhen(true)] object? other) => other is ClusterMemberId id && Equals(in id);

    /// <summary>
    /// Gets the hash code of this identifier.
    /// </summary>
    /// <returns>The hash code of this identifier.</returns>
    public override int GetHashCode() => HashCode.Combine(address, lengthAndPort, family);

    /// <summary>
    /// Returns hexadecimal representation of this identifier.
    /// </summary>
    /// <returns>The hexadecimal representation of this identifier.</returns>
    public override string ToString()
    {
        var writer = new SpanWriter<byte>(stackalloc byte[Size]);
        Format(ref writer);
        return Hex.EncodeToUtf16(writer.WrittenSpan);
    }

    /// <summary>
    /// Attempts to parse cluster member identifier.
    /// </summary>
    /// <param name="identifier">The hexadecimal representation of identifier.</param>
    /// <param name="value">The parsed identifier.</param>
    /// <returns><see langword="true"/> if identifier parsed successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> identifier, out ClusterMemberId value)
    {
        Span<byte> bytes = stackalloc byte[Size];

        bool result;
        value = (result = Hex.DecodeFromUtf16(identifier, bytes) == bytes.Length)
            ? new(bytes)
            : default;
        return result;
    }

    /// <summary>
    /// Attempts to parse cluster member identifier.
    /// </summary>
    /// <param name="identifier">The hexadecimal representation of identifier.</param>
    /// <param name="value">The parsed identifier.</param>
    /// <returns><see langword="true"/> if identifier parsed successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? identifier, out ClusterMemberId value)
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
}