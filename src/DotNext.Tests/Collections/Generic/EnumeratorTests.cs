using System.Buffers;

namespace DotNext.Collections.Generic;

using Buffers;

public sealed class EnumeratorTests : Test
{
    [Fact]
    public static void EmptyMemoryEnumerator()
    {
        using var enumerator = Enumerator.ToEnumerator(ReadOnlyMemory<int>.Empty);
        False(enumerator.MoveNext());
    }

    [Fact]
    public static void ArrayMemoryEnumerator()
    {
        using var enumerator = Enumerator.ToEnumerator(new ReadOnlyMemory<int>([1, 2, 3]));

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
        using var owner = UnmanagedMemoryAllocator.Allocate<int>(3);
        owner[(nint)0] = 10;
        owner[(nint)1] = 20;
        owner[(nint)2] = 30;

        using var enumerator = Enumerator.ToEnumerator<int>(owner.Memory);

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
        using var enumerator = Enumerator.ToEnumerator(ReadOnlySequence<int>.Empty);
        False(enumerator.MoveNext());
    }

    [Fact]
    public static void ArraySequenceEnumerator()
    {
        using var enumerator = Enumerator.ToEnumerator(new ReadOnlySequence<int>(new ReadOnlyMemory<int>(new int[] { 1, 2, 3 })));
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
        using var enumerator = Enumerator.ToEnumerator(ToReadOnlySequence<byte>(bytes, 32));

        var i = 0;
        while (enumerator.MoveNext())
        {
            Equal(bytes[i++], enumerator.Current);
        }
    }

    [Fact]
    public static async Task CanceledAsyncEnumerator()
    {
        await using var enumerator = new int[] { 10, 20, 30 }.GetAsyncEnumerator(new CancellationToken(true));
        await ThrowsAsync<TaskCanceledException>(enumerator.MoveNextAsync().AsTask);
    }

    [Fact]
    public static async Task ConversionToAsyncEnumerator()
    {
        await using var enumerator = new int[] { 10, 20, 30 }.GetAsyncEnumerator();
        for (int index = 0; await enumerator.MoveNextAsync(); index++)
        {
            switch (index)
            {
                case 0:
                    Equal(10, enumerator.Current);
                    break;
                case 1:
                    Equal(20, enumerator.Current);
                    break;
                case 2:
                    Equal(30, enumerator.Current);
                    break;
                default:
                    Fail("Unexpected enumerator state");
                    break;
            }
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
        var range = Enumerable.Range(0, 10);
        await using var enumerator = range.GetAsyncEnumerator();
        True(await enumerator.SkipAsync(8));
        True(await enumerator.MoveNextAsync());
        Equal(8, enumerator.Current);
        True(await enumerator.MoveNextAsync());
        Equal(9, enumerator.Current);
        False(await enumerator.MoveNextAsync());
    }

    [Fact]
    public static void Skip()
    {
        var range = Enumerable.Range(0, 10);
        using var enumerator = range.GetEnumerator();
        True(enumerator.Skip(8));
        True(enumerator.MoveNext());
        Equal(8, enumerator.Current);
        True(enumerator.MoveNext());
        Equal(9, enumerator.Current);
        False(enumerator.MoveNext());
    }
}