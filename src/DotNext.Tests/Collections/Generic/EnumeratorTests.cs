using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic;

using Buffers;

public sealed class EnumeratorTests : Test
{
    [Fact]
    public static void EmptyMemoryEnumerator()
    {
        using var enumerator = MemoryMarshal.ToEnumerator(ReadOnlyMemory<int>.Empty);
        False(enumerator.MoveNext());
    }

    [Fact]
    public static void ArrayMemoryEnumerator()
    {
        using var enumerator = MemoryMarshal.ToEnumerator(new ReadOnlyMemory<int>([1, 2, 3]));

        True(enumerator.MoveNext());
        Equal(1, enumerator.Current);

        True(enumerator.MoveNext());
        Equal(2, enumerator.Current);

        True(enumerator.MoveNext());
        Equal(3, enumerator.Current);

        False(enumerator.MoveNext());
    }

    [Fact]
    public static void NativeMemoryEnumerator()
    {
        using var owner = UnmanagedMemory.Allocate<int>(3);
        owner[0] = 10;
        owner[1] = 20;
        owner[2] = 30;

        using var enumerator = MemoryMarshal.ToEnumerator<int>(owner.Memory);

        True(enumerator.MoveNext());
        Equal(10, enumerator.Current);

        True(enumerator.MoveNext());
        Equal(20, enumerator.Current);

        True(enumerator.MoveNext());
        Equal(30, enumerator.Current);

        False(enumerator.MoveNext());
    }

    [Fact]
    public static void EmptySequenceEnumerator()
    {
        using var enumerator = SequenceMarshal.ToEnumerator(ReadOnlySequence<int>.Empty);
        False(enumerator.MoveNext());
    }

    [Fact]
    public static void ArraySequenceEnumerator()
    {
        using var enumerator = SequenceMarshal.ToEnumerator(new ReadOnlySequence<int>(new ReadOnlyMemory<int>([1, 2, 3])));
        True(enumerator.MoveNext());
        Equal(1, enumerator.Current);

        True(enumerator.MoveNext());
        Equal(2, enumerator.Current);

        True(enumerator.MoveNext());
        Equal(3, enumerator.Current);

        False(enumerator.MoveNext());
    }

    [Fact]
    public static void SequenceEnumerator()
    {
        var bytes = RandomBytes(64);
        using var enumerator = SequenceMarshal.ToEnumerator(ToReadOnlySequence<byte>(bytes, 32));

        var i = 0;
        while (enumerator.MoveNext())
        {
            Equal(bytes[i++], enumerator.Current);
        }
    }

    [Fact]
    public static void SkipValueEnumerator()
    {
        var list = new List<long> { 10L, 20L, 30L };
        var enumerator = list.GetEnumerator();
        True(enumerator.Skip<List<long>.Enumerator, long>(2));
        True(enumerator.MoveNext());
        Equal(30L, enumerator.Current);
        enumerator.Dispose();
    }

    [Fact]
    public static void LimitedSequence()
    {
        var range = Enumerable.Range(0, 10);
        using var enumerator = range.GetEnumerator().Limit(3);
        True(enumerator.MoveNext());
        Equal(0, enumerator.Current);
        True(enumerator.MoveNext());
        Equal(1, enumerator.Current);
        True(enumerator.MoveNext());
        Equal(2, enumerator.Current);
        False(enumerator.MoveNext());
    }

    [Fact]
    public static async Task SkipAsync()
    {
        await using var enumerator = System.Linq.AsyncEnumerable.Range(0, 10).GetAsyncEnumerator(TestToken);
        True(await (enumerator >> 8));
        True(await enumerator.MoveNextAsync());
        Equal(8, enumerator.Current);
        True(await enumerator.MoveNextAsync());
        Equal(9, enumerator.Current);
        False(await enumerator.MoveNextAsync());
    }

    [Fact]
    public static void SkipElements()
    {
        var range = Enumerable.Range(0, 10);
        using var enumerator = range.GetEnumerator();
        True(enumerator >> 8);
        True(enumerator.MoveNext());
        Equal(8, enumerator.Current);
        True(enumerator.MoveNext());
        Equal(9, enumerator.Current);
        False(enumerator.MoveNext());
    }
}