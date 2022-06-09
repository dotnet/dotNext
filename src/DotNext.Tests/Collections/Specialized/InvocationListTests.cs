using System.Diagnostics.CodeAnalysis;

namespace DotNext.Collections.Specialized;

[ExcludeFromCodeCoverage]
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
        list += Predicate.True<object>();
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
        True(list.AsSpan().IsEmpty);

        list += Predicate.True<string>();
        Equal(Predicate.True<string>(), list.AsSpan()[0]);

        list += Predicate.False<object>();
        Equal(2, list.AsSpan().Length);
    }

    [Fact]
    public static void Enumerator()
    {
        InvocationList<Predicate<string>> list = default;

        foreach (var d in list)
        {
            throw new Xunit.Sdk.XunitException();
        }

        list += Predicate.True<string>();

        foreach (var d in list)
        {
            Equal(Predicate.True<string>(), d);
        }

        list += Predicate.False<object>();

        var count = 0;
        foreach (var d in list)
        {
            switch (count++)
            {
                case 0:
                    Equal(Predicate.True<string>(), d);
                    break;
                case 1:
                    Equal(Predicate.False<object>(), d);
                    break;
            }
        }

        Equal(2, count);
    }

    [Fact]
    public static void InterfaceEnumerator()
    {
        var list = InvocationList<Predicate<string>>.Empty;

        Null(list.SingleOrDefault());

        list += Predicate.True<string>();
        Equal(Predicate.True<string>(), list.SingleOrDefault());

        list += Predicate.False<object>();
        Equal(2, list.Count());
    }

    [Fact]
    public static void CombineDelegates()
    {
        var list = InvocationList<Predicate<string>>.Empty;

        Null(list.Combine());

        list += Predicate.True<string>();
        Equal(Predicate.True<string>(), list.Combine());

        list += Predicate.False<string>();
        NotNull(list.Combine());
    }
}