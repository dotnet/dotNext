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

            foreach (var d in list)
            {
                throw new Xunit.Sdk.XunitException();
            }

            list += Predicate.Constant<string>(true);

            foreach (var d in list)
            {
                Same(Predicate.Constant<string>(true), d);
            }

            list += Predicate.Constant<object>(false);

            var count = 0;
            foreach (var d in list)
            {
                switch (count++)
                {
                    case 0:
                        Same(Predicate.Constant<string>(true), d);
                        break;
                    case 1:
                        Same(Predicate.Constant<object>(false), d);
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

            list += Predicate.Constant<string>(true);
            Same(Predicate.Constant<string>(true), list.SingleOrDefault());

            list += Predicate.Constant<object>(false);
            Equal(2, list.Count());
        }

        [Fact]
        public static void CombineDelegates()
        {
            var list = InvocationList<Predicate<string>>.Empty;

            Null(list.Combine());

            list += Predicate.Constant<string>(true);
            Same(Predicate.Constant<string>(true), list.Combine());

            list += Predicate.Constant<string>(false);
            NotNull(list.Combine());
        }
    }
}