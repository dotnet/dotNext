using System.Runtime.CompilerServices;

namespace DotNext.Collections.Specialized;

public sealed class SingletonListTests : Test
{
    [Fact]
    public static void ListInterop()
    {
        IList<int> list = new SingletonList<int> { Item = 42 };
        Equal(42, list[0]);
        True(list.IsReadOnly);
        Equal(42, Single(list));

        list[0] = 52;
        Equal(52, list[0]);

        DoesNotContain(42, list);
        False(list.Contains(42));
        Equal(-1, list.IndexOf(42));

        Contains(52, list);
        Equal(0, list.IndexOf(52));
        True(list.Contains(52));

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
    public static void CollectionInterop()
    {
        IReadOnlyCollection<int> collection = new SingletonList<int> { Item = 42 };
        Equal(42, Single(collection));
    }

    [Fact]
    public static void TupleInterop()
    {
        ITuple tuple = new SingletonList<int> { Item = 42 };
        Equal(1, tuple.Length);
        Equal(42, tuple[0]);
    }

    [Fact]
    public static void EmptyEnumerator()
    {
        False(new SingletonList<int>.Enumerator().MoveNext());
    }
}