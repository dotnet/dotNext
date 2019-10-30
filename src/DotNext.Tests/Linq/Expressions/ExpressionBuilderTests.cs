using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Xunit;

namespace DotNext.Linq.Expressions
{
    [ExcludeFromCodeCoverage]
    public sealed class ExpressionBuilderTests : Assert
    {
        private static Predicate<T> MakeNullCheck<T>()
        {
            var param = Expression.Parameter(typeof(T), "input");
            return Expression.Lambda<Predicate<T>>(param.IsNull(), param).Compile();
        }

        private static Predicate<T> MakeNotNullCheck<T>()
        {
            var param = Expression.Parameter(typeof(T), "input");
            return Expression.Lambda<Predicate<T>>(param.IsNotNull(), param).Compile();
        }

        private static Func<T, string> MakeToString<T>()
        {
            var param = Expression.Parameter(typeof(T), "input");
            return Expression.Lambda<Func<T, string>>(NullSafetyExpression.Create(param, p => p.Call(nameof(ToString))), param).Compile();
        }

        private static Func<T, int?> MakeGetHashCode<T>()
        {
            var param = Expression.Parameter(typeof(T), "input");
            return Expression.Lambda<Func<T, int?>>(NullSafetyExpression.Create(param, p => p.Call(nameof(GetHashCode))), param).Compile();
        }

        private static Func<T, int> MakeGetHashCodeNotNull<T>()
            where T : struct
        {
            var param = Expression.Parameter(typeof(T), "input");
            return Expression.Lambda<Func<T, int>>(NullSafetyExpression.Create(param, p => p.Call(nameof(GetHashCode))), param).Compile();
        }

        [Fact]
        public static void NullCheck()
        {
            var stringPred = MakeNullCheck<string>();
            True(stringPred(null));
            False(stringPred(""));

            var intPred = MakeNullCheck<int>();
            False(intPred(default));

            var nullablePred = MakeNullCheck<int?>();
            True(nullablePred(default));
            False(nullablePred(0));

            var optionalPred = MakeNullCheck<Optional<string>>();
            True(optionalPred(Optional<string>.Empty));
            False(optionalPred(""));
        }

        [Fact]
        public static void NotNullCheck()
        {
            var stringPred = MakeNotNullCheck<string>();
            False(stringPred(null));
            True(stringPred(""));

            var intPred = MakeNotNullCheck<int>();
            True(intPred(default));

            var nullablePred = MakeNotNullCheck<int?>();
            False(nullablePred(default));
            True(nullablePred(0));

            var optionalPred = MakeNotNullCheck<Optional<string>>();
            False(optionalPred(Optional<string>.Empty));
            True(optionalPred(""));
        }

        [Fact]
        public static void NullSafetyToString()
        {
            var intToString = MakeToString<int>();
            Equal("42", intToString(42));

            var nullableToString = MakeToString<int?>();
            Equal("42", nullableToString(42));
            Null(nullableToString(default));

            var optionalToString = MakeToString<Optional<int>>();
            Equal("42", optionalToString(42));
            Null(optionalToString(Optional<int>.Empty));
        }

        [Fact]
        public static void NullSafetyGetHashCode()
        {
            var intHash = MakeGetHashCodeNotNull<int>();
            NotNull(intHash);

            var nullableHash = MakeGetHashCode<int?>();
            NotNull(nullableHash(42));
            Null(nullableHash(default));

            var optionalHash = MakeGetHashCode<Optional<string>>();
            NotNull(optionalHash(""));
            Null(optionalHash(Optional<string>.Empty));
        }

        private delegate ref int RefIntDelegate(TypedReference typedref);

        [Fact]
        public static void RefAnyValExpression()
        {
            var param = Expression.Parameter(typeof(TypedReference));
            var lambda = Expression.Lambda<RefIntDelegate>(param.RefAnyVal<int>(), param).Compile();
            var i = 10;
            Equal(10, lambda(__makeref(i)));
            lambda(__makeref(i)) = 20;
            Equal(20, i);
        }
    }
}