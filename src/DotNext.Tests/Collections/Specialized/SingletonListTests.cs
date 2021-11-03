using System.Diagnostics.CodeAnalysis;

namespace DotNext.Collections.Specialized
{
    [ExcludeFromCodeCoverage]
    public sealed class SingletonListTests : Test
    {
        [Fact]
        public static void ListInterop()
        {
            IList<int> list = new SingletonList<int>(42);
            Equal(42, list[0]);
            True(list.IsReadOnly);
            Equal(1, list.Count);

            list[0] = 52;
            Equal(52, list[0]);

            DoesNotContain(42, list);
            Equal(-1, list.IndexOf(42));

            Contains(52, list);
            Equal(0, list.IndexOf(52));

            var array = new int[1];
            list.CopyTo(array, 0);
            Equal(52, array[0]);

            Throws<IndexOutOfRangeException>(() => list[1] = 62);
            Throws<IndexOutOfRangeException>(() => list[1].CompareTo(52));
            Throws<NotSupportedException>(() => list.Remove(42));
            Throws<NotSupportedException>(() => list.RemoveAt(0));
            Throws<NotSupportedException>(() => list.Add(42));
            Throws<NotSupportedException>(() => list.Insert(0, 42));
        }

        [Fact]
        public static void EmptyEnumerator()
        {
            using var enumerator = new SingletonList<int>.Enumerator();
            False(enumerator.MoveNext());
        }
    }
}