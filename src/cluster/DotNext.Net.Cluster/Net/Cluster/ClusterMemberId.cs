using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Connections;
using static System.Buffers.Binary.BinaryPrimitives;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster;

using Buffers;
using Hex = Buffers.Text.Hex;
using Intrinsics = Runtime.Intrinsics;
using HttpEndPoint = Net.Http.HttpEndPoint;

/// <summary>
/// Represents unique identifier of cluster member.
/// </summary>
#pragma warning disable CA2252  // TODO: Remove in .NET 7
[StructLayout(LayoutKind.Sequential)]
public readonly struct ClusterMemberId : IEquatable<ClusterMemberId>, IBinaryFormattable<ClusterMemberId>
{
    /// <summary>
    /// Gets size of this type, in bytes.
    /// </summary>
    public static int Size => 16 + (sizeof(int) * 3);

    private readonly Guid address;
    private readonly int port, length, family;

    private ClusterMemberId(IPEndPoint endPoint)
    {
        Span<byte> bytes = stackalloc byte[16];
        address = endPoint.Address.TryWriteBytes(bytes, out length)
            ? new(bytes)
            : throw new ArgumentException(ExceptionMessages.UnsupportedAddressFamily, nameof(endPoint));
        port = endPoint.Port;
        family = (int)endPoint.AddressFamily;
    }

    private ClusterMemberId(DnsEndPoint endPoint)
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes.Clear();
        WriteInt64LittleEndian(bytes, Span.BitwiseHashCode64(endPoint.Host.AsSpan(), false));
        address = new(bytes);

        length = endPoint.Host.Length;
        port = endPoint.Port;
        family = (int)endPoint.AddressFamily;
    }

    private ClusterMemberId(HttpEndPoint endPoint)
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes.Clear();
        WriteInt64LittleEndian(bytes, Span.BitwiseHashCode64(endPoint.Host.AsSpan(), false));
        bytes[sizeof(long)] = endPoint.IsSecure.ToByte();
        address = new(bytes);

        length = endPoint.Host.Length;
        port = endPoint.Port;
        family = (int)endPoint.AddressFamily;
    }

    private ClusterMemberId(Uri uri)
    {
        Span<byte> bytes = stackalloc byte[16];
        WriteInt32LittleEndian(bytes, Span.BitwiseHashCode(uri.Scheme.AsSpan(), false));
        WriteInt32LittleEndian(bytes.Slice(sizeof(int)), Span.BitwiseHashCode(uri.Host.AsSpan(), false));
        WriteInt64LittleEndian(bytes.Slice(sizeof(long)), Span.BitwiseHashCode64(uri.PathAndQuery.AsSpan(), false));
        address = new(bytes);

        length = uri.AbsoluteUri.Length;
        port = uri.Port;
        family = (int)uri.HostNameType;
    }

    private ClusterMemberId(SocketAddress address)
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes.Clear();
        WriteInt64LittleEndian(bytes, Intrinsics.GetHashCode64(static (address, index) => address[index], address.Size, address, false));
        this.address = new(bytes);

        port = Intrinsics.GetHashCode32(static (address, index) => address[index], address.Size, address, false);
        family = (int)address.Family;
        length = address.Size;
    }

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
        port = random.Next();
        length = random.Next();
        family = random.Next();
    }

    /// <summary>
    /// Deserializes the cluster member ID.
    /// </summary>
    /// <param name="reader">The memory block reader.</param>
    public ClusterMemberId(ref SpanReader<byte> reader)
    {
        address = new(reader.Read(16));
        port = reader.ReadInt32(true);
        length = reader.ReadInt32(true);
        family = reader.ReadInt32(true);
    }

    /// <inheritdoc cref="IBinaryFormattable{T}.Parse(ref SpanReader{byte})"/>
    static ClusterMemberId IBinaryFormattable<ClusterMemberId>.Parse(ref SpanReader<byte> input)
        => new(ref input);

    /// <summary>
    /// Serializes the value as a sequence of bytes.
    /// </summary>
    /// <param name="writer">The memory block writer.</param>
    /// <exception cref="System.IO.InternalBufferOverflowException"><paramref name="writer"/> is not large enough.</exception>
    public void Format(ref SpanWriter<byte> writer)
    {
        address.TryWriteBytes(writer.Slide(16));
        writer.WriteInt32(port, true);
        writer.WriteInt32(length, true);
        writer.WriteInt32(family, true);
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
            && Unsafe.As<int, ulong>(ref Unsafe.AsRef(in port)) == Unsafe.As<int, ulong>(ref Unsafe.AsRef(in other.port)) // port + length = 8 bytes
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
    public override int GetHashCode() => HashCode.Combine(address, port, length, family);

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
        if (Hex.DecodeFromUtf16(identifier, bytes) == bytes.Length)
        {
            value = new(bytes);
            return true;
        }

        value = default;
        return false;
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
#pragma warning restore CA2252