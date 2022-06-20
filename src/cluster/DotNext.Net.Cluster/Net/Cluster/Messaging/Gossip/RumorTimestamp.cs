using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster.Messaging.Gossip;

using Buffers;

/// <summary>
/// Represents Lamport timestamp of the rumor mixed with the timestamp returned
/// by the local clock.
/// </summary>
#pragma warning disable CA2252  // TODO: Remove in .NET 7
[StructLayout(LayoutKind.Sequential)]
public readonly struct RumorTimestamp : IEquatable<RumorTimestamp>, IBinaryFormattable<RumorTimestamp>, IComparable<RumorTimestamp>
{
    /// <summary>
    /// Represents the serialized size of this value type.
    /// </summary>
    public static int Size => sizeof(long) + sizeof(ulong);

    /// <summary>
    /// Gets the minimum possible value of the timestamp.
    /// </summary>
    public static RumorTimestamp MinValue => new(long.MinValue, ulong.MinValue);

    /// <summary>
    /// Gets the maximum possible value of the timestamp.
    /// </summary>
    public static RumorTimestamp MaxValue => new(long.MaxValue, long.MaxValue);

    private readonly long timestamp;
    private readonly ulong sequenceNumber;

    /// <summary>
    /// Decodes the timestamp from a sequence of bytes.
    /// </summary>
    /// <param name="bytes">The buffer containing encoded timestamp.</param>
    /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="bytes"/> is less than <see cref="Size"/>.</exception>
    public RumorTimestamp(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Size)
            throw new ArgumentOutOfRangeException(nameof(bytes));

        var reader = new SpanReader<byte>(bytes);
        this = new(ref reader);
    }

    /// <summary>
    /// Generates a new fresh timestamp.
    /// </summary>
    public RumorTimestamp()
    {
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        sequenceNumber = ulong.MinValue;
    }

    private RumorTimestamp(ref SpanReader<byte> reader)
    {
        timestamp = reader.ReadInt64(true);
        sequenceNumber = reader.ReadUInt64(true);
    }

    private RumorTimestamp(long timestamp, ulong sequenceNumber)
    {
        this.timestamp = timestamp;
        this.sequenceNumber = sequenceNumber;
    }

    /// <summary>
    /// Serializes the timestamp as a sequence of bytes.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    public void Format(ref SpanWriter<byte> writer)
    {
        writer.WriteInt64(timestamp, true);
        writer.WriteUInt64(sequenceNumber, true);
    }

    /// <summary>
    /// Deserializes the timestamp.
    /// </summary>
    /// <param name="input">The memory block reader.</param>
    /// <returns>The timestamp of the rumor.</returns>
    static RumorTimestamp IBinaryFormattable<RumorTimestamp>.Parse(ref SpanReader<byte> input)
        => new(ref input);

    /// <summary>
    /// Returns an incremented timestamp.
    /// </summary>
    /// <returns>An incremented timestamp.</returns>
    public RumorTimestamp Increment() => new(timestamp, sequenceNumber + 1UL);

    private bool Equals(in RumorTimestamp other)
        => timestamp == other.timestamp && sequenceNumber == other.sequenceNumber;

    /// <summary>
    /// Determines whether the current timestamp is the same as the specified timestamp.
    /// </summary>
    /// <param name="other">The timestamp to compare.</param>
    /// <returns><see langword="true"/> if both timestamps are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(RumorTimestamp other) => Equals(in other);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? other)
        => other is RumorTimestamp id && Equals(in id);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(timestamp, sequenceNumber);

    /// <summary>
    /// Returns hexadecimal representation of this timestamp.
    /// </summary>
    /// <returns>The hexadecimal representation of this timestamp.</returns>
    public override string ToString()
    {
        var writer = new SpanWriter<byte>(stackalloc byte[Size]);
        Format(ref writer);
        return Span.ToHex(writer.WrittenSpan);
    }

    /// <summary>
    /// Restores the timestamp from its hexadecimal representation.
    /// </summary>
    /// <param name="timestamp">The hexadecimal representation of the timestamp.</param>
    /// <param name="result">The parsed timestamp.</param>
    /// <returns><see langword="true"/> if timestamp parsed successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse(ReadOnlySpan<char> timestamp, out RumorTimestamp result)
    {
        Span<byte> bytes = stackalloc byte[Size];

        if (timestamp.FromHex(bytes) == bytes.Length)
        {
            result = new(bytes);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Restores the timestamp from its hexadecimal representation.
    /// </summary>
    /// <param name="timestamp">The hexadecimal representation of the timestamp.</param>
    /// <param name="result">The parsed timestamp.</param>
    /// <returns><see langword="true"/> if timestamp parsed successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryParse([NotNullWhen(true)] string? timestamp, out RumorTimestamp result)
        => TryParse(timestamp.AsSpan(), out result);

    /// <summary>
    /// Determines whether the two timestamps are equal.
    /// </summary>
    /// <param name="x">The first timestamp to compare.</param>
    /// <param name="y">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if both timestamps are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(in RumorTimestamp x, in RumorTimestamp y)
        => x.Equals(in y);

    /// <summary>
    /// Determines whether the two timestamps are not equal.
    /// </summary>
    /// <param name="x">The first timestamp to compare.</param>
    /// <param name="y">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if both timestamps are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(in RumorTimestamp x, in RumorTimestamp y)
        => !x.Equals(y);

    /// <summary>
    /// Compares this timestamp with another one.
    /// </summary>
    /// <param name="other">The timestamp to compare.</param>
    /// <returns>The comparison result.</returns>
    public int CompareTo(RumorTimestamp other)
    {
        var cmp = timestamp.CompareTo(other.timestamp);
        if (cmp is 0)
            cmp = sequenceNumber.CompareTo(other.sequenceNumber);

        return cmp;
    }

    /// <summary>
    /// Determines whether the first timestamp is less than the second one.
    /// </summary>
    /// <param name="x">The first timestamp to compare.</param>
    /// <param name="y">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="x"/> is less than <paramref name="y"/>.</returns>
    public static bool operator <(in RumorTimestamp x, in RumorTimestamp y) => x.timestamp.CompareTo(y.timestamp) switch
    {
        < 0 => true,
        0 => x.sequenceNumber < y.sequenceNumber,
        _ => false,
    };

    /// <summary>
    /// Determines whether the first timestamp is less than or equal to the second one.
    /// </summary>
    /// <param name="x">The first timestamp to compare.</param>
    /// <param name="y">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="x"/> is less than or equal to <paramref name="y"/>.</returns>
    public static bool operator <=(in RumorTimestamp x, in RumorTimestamp y) => x.timestamp.CompareTo(y.timestamp) switch
    {
        < 0 => true,
        0 => x.sequenceNumber <= y.sequenceNumber,
        _ => false,
    };

    /// <summary>
    /// Determines whether the first timestamp is greater than the second one.
    /// </summary>
    /// <param name="x">The first timestamp to compare.</param>
    /// <param name="y">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="x"/> is greater than <paramref name="y"/>.</returns>
    public static bool operator >(in RumorTimestamp x, in RumorTimestamp y) => x.timestamp.CompareTo(y.timestamp) switch
    {
        > 0 => true,
        0 => x.sequenceNumber > y.sequenceNumber,
        _ => false,
    };

    /// <summary>
    /// Determines whether the first timestamp is greater than or equal to the second one.
    /// </summary>
    /// <param name="x">The first timestamp to compare.</param>
    /// <param name="y">The second timestamp to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="x"/> is greater than or equal to <paramref name="y"/>.</returns>
    public static bool operator >=(in RumorTimestamp x, in RumorTimestamp y) => x.timestamp.CompareTo(y.timestamp) switch
    {
        > 0 => true,
        0 => x.sequenceNumber >= y.sequenceNumber,
        _ => false,
    };

    /// <summary>
    /// Generates the next timestamp and stores the modified timestamp at the specified
    /// location atomically.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe.
    /// </remarks>
    /// <param name="location">The location of the timestamp.</param>
    /// <returns>A new timestamp that is greater than previous.</returns>
    public static RumorTimestamp Next(ref RumorTimestamp location)
        => new(location.timestamp, Interlocked.Increment(ref Unsafe.AsRef(in location.sequenceNumber)));
}
#pragma warning restore CA2252