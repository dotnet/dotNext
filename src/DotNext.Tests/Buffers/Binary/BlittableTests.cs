using System.Buffers;

namespace DotNext.Buffers.Binary;

public sealed class BlittableTests : Test
{
    public static TheoryData<ReadOnlySequence<byte>, Guid> TestData
    {
        get
        {
            var result = new TheoryData<ReadOnlySequence<byte>, Guid>();

            var value = Guid.NewGuid();
            var sequence = new ReadOnlySequence<byte>(value.ToByteArray());
            result.Add(sequence, value);

            sequence = ToReadOnlySequence<byte>(value.ToByteArray(), 8);
            result.Add(sequence, value);

            return result;
        }
    }
    
    [Theory]
    [MemberData(nameof(TestData))]
    public static void ParseFromSequence(ReadOnlySequence<byte> source, Guid expected)
    {
        var blittable = Blittable<Guid>.Parse(source);
        Equal(expected, blittable.Value);
    }

    [Fact]
    public static void TryParse()
    {
        var g = Guid.NewGuid();
        True(Blittable<Guid>.TryParse(g.ToByteArray(), out var result));
        Equal(g, result.Value);

        False(Blittable<Guid>.TryParse([], out result));
    }
}