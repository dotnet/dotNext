using System.Diagnostics.CodeAnalysis;

namespace DotNext;

using static Collections.Generic.Sequence;

[ExcludeFromCodeCoverage]
public sealed class OneDimensionalArrayTests : Test
{
    public sealed class Equatable
    {
        private readonly string value;

        public Equatable(string value) => this.value = value;

        public override bool Equals(object other) => other is Equatable equ && value == equ.value;

        public override int GetHashCode() => value.GetHashCode();
    }

    [Fact]
    public static void ArrayEquality2()
    {
        var array1 = new[] { new Equatable("a"), new Equatable("b") };
        var array2 = new[] { new Equatable("a"), new Equatable("b") };
        True(array1.SequenceEqual(array2));
        True(array1.SequenceEqual(array2, true));
        Equal(array1.SequenceHashCode(), array2.SequenceHashCode());
        array2[0] = new Equatable("c");
        False(array1.SequenceEqual(array2));
        False(array1.SequenceEqual(array2, true));
    }

    [Fact]
    public static void Insert()
    {
        int[] array = { 1, 2, 3 };
        Equal(new[] { 1, 4, 2, 3 }, array.Insert(4, (Index)1));
        Equal(new[] { 0, 1, 2, 3 }, array.Insert(0, (Index)0));
        Equal(new[] { 1, 2, 3, 4 }, array.Insert(4, (Index)3));
    }

    [Fact]
    public static void ArrayEquality()
    {
        var array1 = new[] { Guid.Empty, Guid.NewGuid(), Guid.NewGuid() };
        var array2 = new[] { Guid.Empty, array1[1], array1[2] };
        False(array1.Equals(array2));
        True(array1.SequenceEqual(array2));
        True(array1.BitwiseEquals(array2));
        array2[1] = Guid.Empty;
        False(array1.Equals(array2));
        False(array1.SequenceEqual(array2));
        False(array1.BitwiseEquals(array2));
    }

    [Fact]
    public static void BitwiseComparison()
    {
        var array1 = new[] { Guid.Empty, Guid.NewGuid(), Guid.NewGuid() };
        var array2 = new[] { Guid.Empty, array1[1], array1[2] };
        Equal(0, array1.BitwiseCompare(array2));
        array2[1] = Guid.Empty;
        True(array1.BitwiseCompare(array2) > 0);
    }

    [Fact]
    public static void Slice()
    {
        var array = new[] { 1, 2, 3, 4 };
        array = array.Slice(1, 2);
        Equal(2, array.LongLength);
        Equal(2, array[0]);
        Equal(3, array[1]);

        array = new[] { 1, 2, 3, 4 };
        array = array.Slice(0, 2);
        Equal(2, array.LongLength);
        Equal(1, array[0]);
        Equal(2, array[1]);

        array = new[] { 1, 2, 3, 4 };
        array = array.Slice(2, 3);
        Equal(2, array.LongLength);
        Equal(3, array[0]);
        Equal(4, array[1]);
    }

    [Fact]
    public static void View()
    {
        var array = new[] { 1, 2, 3, 4 };
        var view = array.Slice(1..3);
        Equal(2, view.Count);
        Equal(2, view[0]);
        Equal(3, view[1]);

        view = array.Slice(0..2);
        Equal(2, view.Count);
        Equal(1, view[0]);
        Equal(2, view[1]);

        view = array.Slice(2..);
        Equal(2, view.Count);
        Equal(3, view[0]);
        Equal(4, view[1]);
    }

    [Fact]
    public static void Concatenation()
    {
        int[] array1 = { 1, 3, 5 };
        int[] array2 = { 7, 9 };
        Equal(new int[] { 1, 3, 5, 7, 9 }, array1.Concat(array2, array1.Length));
        Equal(array2, array1.Concat(array2, 0));
        Equal(new int[] { 1, 7, 9 }, array1.Concat(array2, 1));
        Equal(Array.Empty<int>(), array1.Concat(Array.Empty<int>(), 0));
        Equal(array2, Array.Empty<int>().Concat(array2, 0));
    }

    [Fact]
    public static void RemoveElement()
    {
        long[] array = { 1, 3, 10 };
        Equal(new[] { 1L, 3L }, array.RemoveAt((Index)2));
        Equal(new[] { 1L }, array.RemoveLast(2));
        Equal(new[] { 10L }, array.RemoveFirst(2));
        Equal(Array.Empty<long>(), array.RemoveFirst(3));
        Equal(Array.Empty<long>(), array.RemoveLast(3));
    }

    [Fact]
    public static unsafe void ForEachUsingPointer()
    {
        int[] array = { 1, 2, 3 };
        array.ForEach(&Exists, array);

        static void Exists(ref int item, int[] array) => Contains(item, array);
    }
}