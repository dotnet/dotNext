using System.Diagnostics.CodeAnalysis;

namespace DotNext.Collections.Specialized
{
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
            True(list.AsSpan().IsEmpty);

            list += Predicate.Constant<string>(true);
            Same(Predicate.Constant<string>(true), list.AsSpan()[0]);

            list += Predicate.Constant<object>(false);
            Equal(2, list.AsSpan().Length);
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
    }
}