using System.Buffers;

namespace DotNext;

using Buffers;

public sealed class SpanTests : Test
{
    [Fact]
    public static void BitwiseEquality()
    {
        Span<Guid> array1 = [Guid.Empty, Guid.NewGuid(), Guid.NewGuid()];
        Span<Guid> array2 = [Guid.Empty, array1[1], array1[2]];
        True(array1.SequenceEqual(array2));
        True(Span.BitwiseEquals<Guid>(array1, array2));
        array2[1] = Guid.Empty;
        False(array1.SequenceEqual(array2));
        False(Span.BitwiseEquals<Guid>(array1, array2));
    }

    [Fact]
    public static void BitwiseComparison()
    {
        Span<Guid> array1 = [Guid.Empty, Guid.NewGuid(), Guid.NewGuid()];
        Span<Guid> array2 = [Guid.Empty, array1[1], array1[2]];
        Equal(0, Span.BitwiseCompare<Guid>(array1, array2));
        array2[1] = Guid.Empty;
        True(Span.BitwiseCompare<Guid>(array1, array2) > 0);
    }

    [Fact]
    public static unsafe void SortingUsingPointer()
    {
        Span<ulong> span = [3, 2, 6, 4];
        span.Sort(&Sort);
        Equal(6UL, span[0]);
        Equal(4UL, span[1]);
        Equal(3UL, span[2]);
        Equal(2UL, span[3]);

        static int Sort(ulong x, ulong y) => (int)(y - x);
    }

    [Fact]
    public static void IndexOf()
    {
        ReadOnlySpan<ulong> span = stackalloc ulong[] { 3, 2, 6, 4 };
        Equal(1, span.IndexOf(2UL, 0, EqualityComparer<ulong>.Default.Equals));
        Equal(3, span.IndexOf(4UL, 0, EqualityComparer<ulong>.Default.Equals));
        Equal(3UL, span[0]);
        Equal(2UL, span[1]);
    }

    [Fact]
    public static void ForEach()
    {
        Span<long> span = stackalloc long[] { 3, 2, 6, 4 };
        span.ForEach((ref long value, int index) => value += index);
        Equal(3, span[0]);
        Equal(3, span[1]);
        Equal(8, span[2]);
        Equal(7, span[3]);
    }

    [Fact]
    public static void TrimByLength1()
    {
        Span<int> span = [1, 2, 3];
        span = span.TrimLength(4);
        Equal(3, span.Length);
        span = span.TrimLength(2);
        Equal(2, span.Length);
        Equal(1, span[0]);
        Equal(2, span[1]);
    }

    [Fact]
    public static void TrimByLength2()
    {
        ReadOnlySpan<int> span = stackalloc int[] { 1, 2, 3 };
        span = span.TrimLength(4);
        Equal(3, span.Length);
        span = span.TrimLength(2);
        Equal(2, span.Length);
        Equal(1, span[0]);
        Equal(2, span[1]);
    }

    public static TheoryData<MemoryAllocator<char>> TestAllocators() => new()
    {
        null,
        Memory.GetArrayAllocator<char>(),
        ArrayPool<char>.Shared.ToAllocator(),
    };

    [Theory]
    [MemberData(nameof(TestAllocators))]
    public static void Concatenation(MemoryAllocator<char> allocator)
    {
        MemoryOwner<char> owner = string.Empty.AsSpan().Concat(string.Empty, allocator);
        True(owner.IsEmpty);
        True(owner.Memory.IsEmpty);
        owner.Dispose();

        owner = "Hello, ".AsSpan().Concat("world!", allocator);
        False(owner.IsEmpty);
        False(owner.Memory.IsEmpty);
        Equal("Hello, world!", new string(owner.Span));
        owner.Dispose();

        owner = "Hello, ".AsSpan().Concat("world", "!", allocator);
        False(owner.IsEmpty);
        False(owner.Memory.IsEmpty);
        Equal("Hello, world!", new string(owner.Span));
        owner.Dispose();
    }

    [Theory]
    [InlineData(new int[] { 10, 20, 30 }, new int[] { 0, 0, 0 })]
    [InlineData(new int[] { 10, 20, 30 }, new int[] { 0, 0 })]
    [InlineData(new int[] { 10, 20, 30 }, new int[] { 0, 0, 0, 0 })]
    public static void Copy(int[] source, int[] destination)
    {
        ReadOnlySpan<int> src = source;
        Span<int> dst = destination;
        src.CopyTo(dst, out var writtenCount);
        Equal(Math.Min(src.Length, dst.Length), writtenCount);

        for (var i = 0; i < writtenCount; i++)
            Equal(src[i], dst[i]);
    }

    [Fact]
    public static void BufferizeSpan()
    {
        var owner = ReadOnlySpan<byte>.Empty.Copy();
        True(owner.IsEmpty);

        owner = new ReadOnlySpan<byte>(new byte[] { 10, 20, 30 }).Copy();
        Equal(3, owner.Length);
        Equal(10, owner[0]);
        Equal(20, owner[1]);
        Equal(30, owner[2]);
        owner.Dispose();
    }

    [Fact]
    public static void Tuple0ToSpan()
    {
        var tuple = new ValueTuple();
        True(tuple.AsSpan<int>().IsEmpty);
    }

    [Fact]
    public static void Tuple0ToReadOnlySpan()
    {
        var tuple = new ValueTuple();
        True(tuple.AsReadOnlySpan<int>().IsEmpty);
    }

    [Fact]
    public static void Tuple1ToSpan()
    {
        var tuple = new ValueTuple<int>(42);
        var span = tuple.AsSpan();
        False(span.IsEmpty);
        Equal(1, span.Length);
        Equal(42, span[0]);

        span[0] = 52;
        Equal(52, tuple.Item1);
    }

    [Fact]
    public static void Tuple1ToReadOnlySpan()
    {
        var tuple = new ValueTuple<int>(42);
        var span = tuple.AsReadOnlySpan();
        False(span.IsEmpty);
        Equal(1, span.Length);
        Equal(42, span[0]);
    }

    [Fact]
    public static void Tuple2ToSpan()
    {
        var tuple = (42, 43);
        var span = tuple.AsSpan();
        False(span.IsEmpty);
        Equal(2, span.Length);
        Equal(42, span[0]);
        Equal(43, span[1]);

        span[0] = 52;
        span[1] = 53;
        Equal(52, tuple.Item1);
        Equal(53, tuple.Item2);
    }

    [Fact]
    public static void Tuple2ToReadOnlySpan()
    {
        var tuple = (42, 43);
        var span = tuple.AsReadOnlySpan();
        False(span.IsEmpty);
        Equal(2, span.Length);
        Equal(42, span[0]);
        Equal(43, span[1]);
    }

    [Fact]
    public static void Tuple3ToSpan()
    {
        var tuple = (42, 43, 44);
        var span = tuple.AsSpan();
        False(span.IsEmpty);
        Equal(3, span.Length);
        Equal(42, span[0]);
        Equal(43, span[1]);
        Equal(44, span[2]);

        span[0] = 52;
        span[1] = 53;
        span[2] = 54;
        Equal(52, tuple.Item1);
        Equal(53, tuple.Item2);
        Equal(54, tuple.Item3);
    }

    [Fact]
    public static void Tuple3ToReadOnlySpan()
    {
        var tuple = (42, 43, 44);
        var span = tuple.AsReadOnlySpan();
        False(span.IsEmpty);
        Equal(3, span.Length);
        Equal(42, span[0]);
        Equal(43, span[1]);
        Equal(44, span[2]);
    }

    [Fact]
    public static void Tuple4ToSpan()
    {
        var tuple = (42, 43, 44, 45);
        var span = tuple.AsSpan();
        False(span.IsEmpty);
        Equal(4, span.Length);
        Equal(42, span[0]);
        Equal(43, span[1]);
        Equal(44, span[2]);
        Equal(45, span[3]);

        span[0] = 52;
        span[1] = 53;
        span[2] = 54;
        span[3] = 55;
        Equal(52, tuple.Item1);
        Equal(53, tuple.Item2);
        Equal(54, tuple.Item3);
        Equal(55, tuple.Item4);
    }

    [Fact]
    public static void Tuple4ToReadOnlySpan()
    {
        var tuple = (42, 43, 44, 45);
        var span = tuple.AsReadOnlySpan();
        False(span.IsEmpty);
        Equal(4, span.Length);
        Equal(42, span[0]);
        Equal(43, span[1]);
        Equal(44, span[2]);
        Equal(45, span[3]);
    }

    [Fact]
    public static void Tuple5ToSpan()
    {
        var tuple = (42, 43, 44, 45, 46);
        var span = tuple.AsSpan();
        False(span.IsEmpty);
        Equal(5, span.Length);
        Equal(42, span[0]);
        Equal(43, span[1]);
        Equal(44, span[2]);
        Equal(45, span[3]);
        Equal(46, span[4]);

        span[0] = 52;
        span[1] = 53;
        span[2] = 54;
        span[3] = 55;
        span[4] = 56;
        Equal(52, tuple.Item1);
        Equal(53, tuple.Item2);
        Equal(54, tuple.Item3);
        Equal(55, tuple.Item4);
        Equal(56, tuple.Item5);
    }

    [Fact]
    public static void Tuple5ToReadOnlySpan()
    {
        var tuple = (42, 43, 44, 45, 46);
        var span = tuple.AsReadOnlySpan();
        False(span.IsEmpty);
        Equal(5, span.Length);
        Equal(42, span[0]);
        Equal(43, span[1]);
        Equal(44, span[2]);
        Equal(45, span[3]);
        Equal(46, span[4]);
    }

    [Fact]
    public static void Tuple6ToSpan()
    {
        var tuple = (42, 43, 44, 45, 46, 47);
        var span = tuple.AsSpan();
        False(span.IsEmpty);
        Equal(6, span.Length);
        Equal(42, span[0]);
        Equal(43, span[1]);
        Equal(44, span[2]);
        Equal(45, span[3]);
        Equal(46, span[4]);
        Equal(47, span[5]);

        span[0] = 52;
        span[1] = 53;
        span[2] = 54;
        span[3] = 55;
        span[4] = 56;
        span[5] = 57;
        Equal(52, tuple.Item1);
        Equal(53, tuple.Item2);
        Equal(54, tuple.Item3);
        Equal(55, tuple.Item4);
        Equal(56, tuple.Item5);
        Equal(57, tuple.Item6);
    }

    [Fact]
    public static void Tuple6ToReadOnlySpan()
    {
        var tuple = (42, 43, 44, 45, 46, 47);
        var span = tuple.AsReadOnlySpan();
        False(span.IsEmpty);
        Equal(6, span.Length);
        Equal(42, span[0]);
        Equal(43, span[1]);
        Equal(44, span[2]);
        Equal(45, span[3]);
        Equal(46, span[4]);
        Equal(47, span[5]);
    }

    [Fact]
    public static void Tuple7ToSpan()
    {
        var tuple = (42, 43, 44, 45, 46, 47, 48);
        var span = tuple.AsSpan();
        False(span.IsEmpty);
        Equal(7, span.Length);
        Equal(42, span[0]);
        Equal(43, span[1]);
        Equal(44, span[2]);
        Equal(45, span[3]);
        Equal(46, span[4]);
        Equal(47, span[5]);
        Equal(48, span[6]);

        span[0] = 52;
        span[1] = 53;
        span[2] = 54;
        span[3] = 55;
        span[4] = 56;
        span[5] = 57;
        span[6] = 58;
        Equal(52, tuple.Item1);
        Equal(53, tuple.Item2);
        Equal(54, tuple.Item3);
        Equal(55, tuple.Item4);
        Equal(56, tuple.Item5);
        Equal(57, tuple.Item6);
        Equal(58, tuple.Item7);
    }

    [Fact]
    public static void Tuple7ToReadOnlySpan()
    {
        var tuple = (42, 43, 44, 45, 46, 47, 48);
        var span = tuple.AsReadOnlySpan();
        False(span.IsEmpty);
        Equal(7, span.Length);
        Equal(42, span[0]);
        Equal(43, span[1]);
        Equal(44, span[2]);
        Equal(45, span[3]);
        Equal(46, span[4]);
        Equal(47, span[5]);
        Equal(48, span[6]);
    }

    [Fact]
    public static unsafe void ForEachUsingPointer()
    {
        int[] array = { 1, 2, 3 };
        array.AsSpan().ForEach(&Exists, array);

        static void Exists(ref int item, int[] array) => Contains(item, array);
    }

    [Fact]
    public static void ConcatStrings()
    {
        using (var buffer = Span.Concat(default(ValueTuple).AsReadOnlySpan<string>()))
        {
            Empty(buffer.Span.ToString());
        }

        var tuple1 = new ValueTuple<string>("Hello, world!");
        using (var buffer = Span.Concat(tuple1.AsReadOnlySpan()))
        {
            Equal("Hello, world!", buffer.Span.ToString());
        }

        var tuple2 = ("Hello, ", "world!");
        using (var buffer = Span.Concat(tuple2.AsReadOnlySpan()))
        {
            Equal("Hello, world!", buffer.Span.ToString());
        }
    }

    [Fact]
    public static void SpanContravariance()
    {
        ReadOnlySpan<IEnumerable<char>> sequence = new ReadOnlySpan<string>(new string[] { "a", "b" }).Contravariance<string, IEnumerable<char>>();
        Contains('a', sequence[0]);
        Contains('b', sequence[1]);
    }

    [Fact]
    public static void SplitSpanByLength()
    {
        Span<char> chars = ['a', 'b', 'c', 'd'];
        var head = chars.TrimLength(2, out var rest);
        Equal("ab", head.ToString());
        Equal("cd", rest.ToString());

        head = chars.TrimLength(0, out rest);
        Equal(string.Empty, head.ToString());
        Equal(chars.ToString(), rest.ToString());

        head = chars.TrimLength(chars.Length, out rest);
        Equal(chars.ToString(), head.ToString());
        Equal(string.Empty, rest.ToString());
    }

    [Fact]
    public static void SwapElements()
    {
        Span<byte> expected = RandomBytes(SpanOwner<byte>.StackallocThreshold * 4 + 2);
        Span<byte> actual = expected.ToArray();
        var midpoint = actual.Length >> 1;
        actual.Slice(0, midpoint).Swap(actual.Slice(midpoint));
        Equal(expected.Slice(midpoint), actual.Slice(0, midpoint));
        Equal(expected.Slice(0, midpoint), actual.Slice(midpoint));
    }

    [Fact]
    public static void TransformElements()
    {
        // left < right
        Span<int> input = [1, 2, 3, 4, 5, 6];
        input.Swap(0..2, 3..6);
        Equal(stackalloc int[] { 4, 5, 6, 3, 1, 2 }, input);

        // left > right
        input = [1, 2, 3, 4, 5, 6];
        input.Swap(0..3, 4..6);
        Equal(stackalloc int[] { 5, 6, 4, 1, 2, 3 }, input);

        // left is zero length
        input = [1, 2, 3, 4, 5, 6];
        input.Swap(1..1, 3..6);
        Equal(stackalloc int[] { 1, 4, 5, 6, 2, 3 }, input);

        // right is zero length
        input = [1, 2, 3, 4, 5, 6];
        input.Swap(0..2, 3..3);
        Equal(stackalloc int[] { 3, 1, 2, 4, 5, 6 }, input);

        // no space between ranges
        input = [1, 2, 3, 4, 5, 6];
        input.Swap(0..2, 2..6);
        Equal(stackalloc int[] { 3, 4, 5, 6, 1, 2 }, input);

        // left == right
        input = [1, 2, 3, 4, 5, 6];
        input.Swap(0..3, 3..6);
        Equal(stackalloc int[] { 4, 5, 6, 1, 2, 3 }, input);

        // left and right are empty
        input = [1, 2, 3, 4, 5, 6];
        input.Swap(1..1, 5..5);
        Equal(stackalloc int[] { 1, 2, 3, 4, 5, 6 }, input);

        // overlapping
        Throws<ArgumentException>(() => new int[] { 1, 2, 3, 4, 5, 6 }.AsSpan().Swap(0..2, 1..3));
    }

    [Fact]
    public static void MoveRange()
    {
        // move from left to right
        Span<int> input = [1, 2, 3, 4, 5, 6];
        input.Move(0..2, 3);
        Equal(stackalloc int[] { 3, 1, 2, 4, 5, 6 }, input);

        // move from left to right
        input = [1, 2, 3, 4, 5, 6];
        input.Move(1..3, 6);
        Equal(stackalloc int[] { 1, 4, 5, 6, 2, 3 }, input);

        // move from right to left
        input.Move(4..6, 1);
        Equal(stackalloc int[] { 1, 2, 3, 4, 5, 6 }, input);

        // out of range
        Throws<ArgumentOutOfRangeException>(() => new int[] { 1, 2, 3, 4, 5, 6 }.AsSpan().Move(0..2, 1));
    }

    [Fact]
    public static void AdvanceReadOnlySpan()
    {
        ReadOnlySpan<int> array = new int[] { 10, 20, 30 };
        Equal(10, array.Advance());
        Equal(new int[] { 20, 30 }, array.Advance(2));
    }

    [Fact]
    public static void AdvanceSpan()
    {
        Span<int> array = [10, 20, 30];
        Equal(10, array.Advance());
        Equal([20, 30], array.Advance(2));
    }
    
    [Fact]
    public static void CheckMask()
    {
        ReadOnlySpan<byte> value = [1, 1, 0];
        
        True(value.CheckMask<byte>([0, 1, 0]));
        True(value.CheckMask<byte>([1, 1, 0]));
        False(value.CheckMask<byte>([0, 0, 1]));
    }

    [Fact]
    public static void CheckLargeMask()
    {
        var value = RandomBytes(1024);
        if (value[0] is byte.MaxValue)
            value[0] = byte.MaxValue - 1;
        
        var mask = value.AsSpan().ToArray();
        mask.AsSpan(0, 512).Clear();

        True(new ReadOnlySpan<byte>(value).CheckMask(mask));
        mask[0] = (byte)~value[0];

        False(new ReadOnlySpan<byte>(value).CheckMask(mask));
    }

    [Fact]
    public static void IsBitwiseAndNonZero()
    {
        ReadOnlySpan<byte> value = [1, 1, 0];

        True(value.IsBitwiseAndNonZero<byte>([0, 1, 1]));
        True(value.IsBitwiseAndNonZero<byte>([1, 1, 1]));
        False(value.IsBitwiseAndNonZero<byte>([0, 0, 1]));
    }
    
    [Fact]
    public static void IsBitwiseAndNonZeroLargeMask()
    {
        var value = RandomBytes(1024);
        var mask = new byte[value.Length];

        for (var i = 0; i < value.Length; i++)
        {
            var b = value[i];
            if ((b & 1) is 1)
            {
                mask[i] = b;
                break;
            }
        }

        True(new ReadOnlySpan<byte>(value).IsBitwiseAndNonZero(mask));
        
        Array.Clear(mask);
        False(new ReadOnlySpan<byte>(value).IsBitwiseAndNonZero(mask));
    }
}