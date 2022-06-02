using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Messaging.Gossip;

using Buffers;

/// <summary>
/// Represents transient identifier that is stable during the application lifetime.
/// The identifier can be used to organize rumor spreading.
/// </summary>
#pragma warning disable CA2252  // TODO: Remove in .NET 7
[StructLayout(LayoutKind.Sequential)]
public readonly struct PeerTransientId : IEquatable<PeerTransientId>, IBinaryFormattable<PeerTransientId>
{
    /// <summary>
    /// Gets the serialized size of this value type.
    /// </summary>
    public const int Size = sizeof(long) + sizeof(ulong);

    private readonly long timestampPart;
    private readonly ulong randomPart;

    /// <summary>
    /// Generates a new transient identifier.
    /// </summary>
    /// <param name="random">The source of random values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> is <see langword="null"/>.</exception>
    public PeerTransientId(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        randomPart = random.Next<ulong>();
        timestampPart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Decodes identifier from a sequence of bytes.
    /// </summary>
    /// <param name="bytes">The buffer containing encoded identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="bytes"/> is less than <see cref="Size"/>.</exception>
    public PeerTransientId(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Size)
            throw new ArgumentOutOfRangeException(nameof(bytes));

        var reader = new SpanReader<byte>(bytes);
        this = new(ref reader);
    }

    private PeerTransientId(ref SpanReader<byte> reader)
    {
        timestampPart = reader.ReadInt64(true);
        randomPart = reader.ReadUInt64(true);
    }

    /// <summary>
    /// Gets a date/time when this ID was generated.
    /// </summary>
    /// <remarks>
    /// Timestamp is a part of the identifier so it can be passed across
    /// application bounderies using serialization.
    /// </remarks>
    public DateTimeOffset CreatedAt => DateTimeOffset.FromUnixTimeMilliseconds(timestampPart);

    /// <summary>
    /// Serializes the identifier as a sequence of bytes.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    public void Format(ref SpanWriter<byte> writer)
    {
        writer.WriteInt64(timestampPart, true);
        writer.WriteUInt64(randomPart, true);
    }

    /// <inheritdoc />
    static int IBinaryFormattable<PeerTransientId>.Size => Size;

    /// <inheritdoc />
    static PeerTransientId IBinaryFormattable<PeerTransientId>.Parse(ref SpanReader<byte> input)
        => new(ref input);

    private bool Equals(in PeerTransientId other)
        => timestampPart == other.timestampPart && randomPart == other.randomPart;

    /// <summary>
    /// Determines whether the current identifier is the same as the specified identifier.
    /// </summary>
    /// <param name="other">The identifier to compare.</param>
    /// <returns><see langword="true"/> if both identifiers are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(PeerTransientId other) => Equals(in other);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? other)
        => other is PeerTransientId id && Equals(in id);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(timestampPart, randomPart);

    /// <summary>
    /// Returns hexadecimal representation of this identifier.
    /// </summary>
    /// <returns>The hexadecimal representation of this identifier.</returns>
    public override string ToString()
    {
        var writer = new SpanWriter<byte>(stackalloc byte[Size]);
        Format(ref writer);
        return Span.ToHex(writer.WrittenSpan);
    }

    /// <summary>
    /// Restores the identifier from its hexadecimal representation.
    /// </summary>
    /// <param name="identifier">The hexadecimal representation of the identifier.</param>
    /// <param name="result">The parsed identifier.</param>
    /// <returns><see langword="true"/> if identifier parsed successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> identifier, out PeerTransientId result)
    {
        Span<byte> bytes = stackalloc byte[Size];

        if (identifier.FromHex(bytes) == bytes.Length)
        {
            result = new(bytes);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Restores the identifier from its hexadecimal representation.
    /// </summary>
    /// <param name="identifier">The hexadecimal representation of the identifier.</param>
    /// <param name="result">The parsed identifier.</param>
    /// <returns><see langword="true"/> if identifier parsed successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? identifier, out PeerTransientId result)
        => TryParse(identifier.AsSpan(), out result);

    /// <summary>
    /// Determines whether the two identifiers are equal.
    /// </summary>
    /// <param name="x">The first identifier to compare.</param>
    /// <param name="y">The second identifier to compare.</param>
    /// <returns><see langword="true"/> if both identifiers are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(in PeerTransientId x, in PeerTransientId y)
        => x.Equals(in y);

    /// <summary>
    /// Determines whether the two identifiers are not equal.
    /// </summary>
    /// <param name="x">The first identifier to compare.</param>
    /// <param name="y">The second identifier to compare.</param>
    /// <returns><see langword="true"/> if both identifiers are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(in PeerTransientId x, in PeerTransientId y)
        => !x.Equals(y);
}
#pragma warning restore CA2252