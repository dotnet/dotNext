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
        list += Predicate<object>.Constant(true);
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

        list += Predicate<string>.Constant(true);
        Same(Predicate<string>.Constant(true), list.Span[0]);

        list += Predicate<object>.Constant(false);
        Equal(2, list.Span.Length);
    }

    [Fact]
    public static void Enumerator()
    {
        InvocationList<Predicate<string>> list = default;
        Empty(list);

        list += Predicate<string>.Constant(true);
        Collection(list, Same(Predicate<string>.Constant(true)));

        list += Predicate<string>.Constant(false);
        Collection(
            list,
            Same(Predicate<string>.Constant(true)),
            Same(Predicate<string>.Constant(false)));
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