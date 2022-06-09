using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Messaging.Gossip;

using Buffers;

[ExcludeFromCodeCoverage]
public sealed class RumorIdTests : Test
{
    [Fact]
    public static void Equality()
    {
        var id1 = new RumorTimestamp();
        var id2 = id1.Increment();
        NotEqual(id1, id2);
        False(id1 == id2);
        True(id1 != id2);
        False(id1.Equals(id2));
        id2 = id1;
        Equal(id1, id2);
        True(id1 == id2);
        False(id1 != id2);
        True(id1.Equals(id2));
    }

    [Fact]
    public static void Comparison1()
    {
        var id1 = new RumorTimestamp();
        var id2 = id1.Increment();

        True(id1 != id2);
        False(id1 == id2);
        True(id1.CompareTo(id2) < 0);

        True(id1 < id2);
        True(id1 <= id2);

        False(id1 > id2);
        False(id1 >= id2);
    }

    [Fact]
    public static void Comparison2()
    {
        var id1 = new RumorTimestamp();
        Thread.Sleep(50);
        var id2 = new RumorTimestamp();

        True(id1 != id2);
        False(id1 == id2);
        True(id1.CompareTo(id2) < 0);

        True(id1 < id2);
        True(id1 <= id2);

        False(id1 > id2);
        False(id1 >= id2);
    }

    [Fact]
    public static void RestoreFromBytes()
    {
        var id1 = new RumorTimestamp();
        Span<byte> bytes = stackalloc byte[RumorTimestamp.Size];
        var writer = new SpanWriter<byte>(bytes);
        id1.Format(ref writer);

        var id2 = new RumorTimestamp(bytes);
        Equal(id1, id2);
    }

    [Fact]
    public static void Parsing()
    {
        var expected = new RumorTimestamp();
        True(RumorTimestamp.TryParse(expected.ToString().AsSpan(), out var actual));
        Equal(expected, actual);
        var invalidHex = "AB142244";
        False(RumorTimestamp.TryParse(invalidHex.AsSpan(), out _));
    }

    [Fact]
    public static void MinMaxValue()
    {
        True(RumorTimestamp.MinValue < RumorTimestamp.MaxValue);
        True(RumorTimestamp.MinValue <= RumorTimestamp.MaxValue);
        True(RumorTimestamp.MinValue.CompareTo(RumorTimestamp.MaxValue) < 0);
    }
}