namespace DotNext.Collections.Specialized;

public sealed class SingletonListTests : Test
{
    [Fact]
    public static void ListInterop()
    {
        IList<int> list = new SingletonList<int> { Item = 42 };
        Equal(42, list[0]);
        True(list.IsReadOnly);
        Single(list);

        list[0] = 52;
        Equal(52, list[0]);

        DoesNotContain(42, list);
        Equal(-1, list.IndexOf(42));

        Contains(52, list);
        Equal(0, list.IndexOf(52));

        var array = new int[1];
        list.CopyTo(array, 0);
        Equal(52, array[0]);

        Throws<ArgumentOutOfRangeException>(() => list[1] = 62);
        Throws<ArgumentOutOfRangeException>(() => list[1].CompareTo(52));
        Throws<NotSupportedException>(() => list.Remove(42));
        Throws<NotSupportedException>(() => list.RemoveAt(0));
        Throws<NotSupportedException>(() => list.Add(42));
        Throws<NotSupportedException>(() => list.Insert(0, 42));
        Throws<NotSupportedException>(list.Clear);
    }

    [Fact]
    public static void EmptyEnumerator()
    {
        False(new SingletonList<int>.Enumerator().MoveNext());
    }
}