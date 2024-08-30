using System.Runtime.CompilerServices;

namespace DotNext.Collections.Specialized;

public sealed class InvocationListTests : Test
{
    [Fact]
    public static void Operators()
    {
        InvocationList<Predicate<string>> list = default;
        True(list.IsEmpty);
        Empty(list);

        list += static str => str.Length > 10;
        list += CheckLength;
        list += Predicate.Constant<object>(true);
        NotEmpty(list);
        False(list.IsEmpty);
        Equal(3, list.Count);

        list -= CheckLength;
        Equal(2, list.Count);

        static bool CheckLength(object obj) => obj is string { Length: > 10 };
    }

    [Fact]
    public static void GetInvocationList()
    {
        InvocationList<Predicate<string>> list = default;
        True(list.Span.IsEmpty);

        list += Predicate.Constant<string>(true);
        Same(Predicate.Constant<string>(true), list.Span[0]);

        list += Predicate.Constant<object>(false);
        Equal(2, list.Span.Length);
    }

    [Fact]
    public static void Enumerator()
    {
        InvocationList<Predicate<string>> list = default;
        Empty(list);

        list += Predicate.Constant<string>(true);
        Collection(list, Same(Predicate.Constant<string>(true)));

        list += Predicate.Constant<object>(false);
        Collection(
            list,
            Same(Predicate.Constant<string>(true)),
            Same<Predicate<string>>(Predicate.Constant<object>(false)));
    }

    [Fact]
    public static void Combine()
    {
        var box = new StrongBox<int>();
        var list = new InvocationList<Action>() + Inc + Inc;
        list.Combine()?.Invoke();
        Equal(2, box.Value);

        list = default;
        Null(list.Combine());

        void Inc() => box.Value += 1;
    }
}