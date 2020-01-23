using System;
using System.Collections.Generic;
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

        [Fact]
        public static void AndBuilder()
        {
            var expr = true.Const().And(false.Const());
            Equal(ExpressionType.And, expr.NodeType);
            Equal(ExpressionType.Constant, expr.Left.NodeType);
            Equal(ExpressionType.Constant, expr.Right.NodeType);
        }

        [Fact]
        public static void AssignToIndexer()
        {
            var item = typeof(IList<int>).GetProperty("Item");
            NotNull(item);
            var indexer = Expression.MakeIndex(Expression.Constant(new int[0]), item, new[] { 0.Const() });
            var expr = indexer.Assign(42.Const());
            Equal(ExpressionType.Assign, expr.NodeType);
            Equal(ExpressionType.Constant, expr.Right.NodeType);
            Equal(ExpressionType.Index, expr.Left.NodeType);
            expr = indexer.AssignDefault();
            Equal(ExpressionType.Assign, expr.NodeType);
            Equal(ExpressionType.Default, expr.Right.NodeType);
            Equal(ExpressionType.Index, expr.Left.NodeType);
        }

        [Fact]
        public static void AssignToVariable()
        {
            var expr = Expression.Parameter(typeof(long)).AssignDefault();
            Equal(ExpressionType.Assign, expr.NodeType);
            Equal(ExpressionType.Default, expr.Right.NodeType);
        }

        [Fact]
        public static void GotoLabel()
        {
            var label = Expression.Label();
            var expr = label.Break();
            Equal(ExpressionType.Goto, expr.NodeType);
            Equal(GotoExpressionKind.Break, expr.Kind);

            expr = label.Continue();
            Equal(ExpressionType.Goto, expr.NodeType);
            Equal(GotoExpressionKind.Continue, expr.Kind);

            expr = label.Goto();
            Equal(ExpressionType.Goto, expr.NodeType);
            Equal(GotoExpressionKind.Goto, expr.Kind);

            expr = label.Return();
            Equal(ExpressionType.Goto, expr.NodeType);
            Equal(GotoExpressionKind.Return, expr.Kind);

            label = Expression.Label(typeof(int));
            expr = label.Break(typeof(int).Default());
            Equal(ExpressionType.Goto, expr.NodeType);
            Equal(GotoExpressionKind.Break, expr.Kind);

            expr = label.Goto(42.Const());
            Equal(ExpressionType.Goto, expr.NodeType);
            Equal(GotoExpressionKind.Goto, expr.Kind);

            expr = label.Return(42.Const());
            Equal(ExpressionType.Goto, expr.NodeType);
            Equal(GotoExpressionKind.Return, expr.Kind);

            var site = label.LandingSite(42.Const());
            Equal(ExpressionType.Label, site.NodeType);
            Equal(ExpressionType.Constant, site.DefaultValue.NodeType);
        }

        [Fact]
        public static void ArrayElement()
        {
            var indexer = new int[0].Const().ElementAt(1.Const());
            Equal(ExpressionType.Index, indexer.NodeType);
            Equal(ExpressionType.Constant, indexer.Object.NodeType);
        }

        [Fact]
        public static void InvokeDelegate()
        {
            var expr = new Action(ArrayElement).Const().Invoke();
            Equal(ExpressionType.Invoke, expr.NodeType);
            Equal(ExpressionType.Constant, expr.Expression.NodeType);
        }

        [Fact]
        public static void VariousOperators()
        {
            var expr = 42.Const().GreaterThanOrEqual(43.Const());
            Equal(ExpressionType.GreaterThanOrEqual, expr.NodeType);

            expr = 42.Const().LeftShift(2.Const());
            Equal(ExpressionType.LeftShift, expr.NodeType);

            expr = 42.Const().RightShift(2.Const());
            Equal(ExpressionType.RightShift, expr.NodeType);

            expr = 42.Const().LessThanOrEqual(43.Const());
            Equal(ExpressionType.LessThanOrEqual, expr.NodeType);

            expr = 42.Const().Modulo(43.Const());
            Equal(ExpressionType.Modulo, expr.NodeType);

            expr = 42.Const().NotEqual(43.Const());
            Equal(ExpressionType.NotEqual, expr.NodeType);

            expr = 42.Const().Xor(43.Const());
            Equal(ExpressionType.ExclusiveOr, expr.NodeType);

            expr = 42.Const().Or(43.Const());
            Equal(ExpressionType.Or, expr.NodeType);

            expr = 42D.Const().Power(2D.Const());
            Equal(ExpressionType.Power, expr.NodeType);
        }

        [Fact]
        public static void ThrowExceptionExpr()
        {
            var expr = typeof(Exception).New().Throw();
            Equal(typeof(void), expr.Type);
            Equal(ExpressionType.Throw, expr.NodeType);
        }

        [Fact]
        public static void NewString()
        {
            var expr = typeof(string).Const().New('a'.Const().Convert<object>(), 2.Const().Convert<object>());
            var lambda = Expression.Lambda<Func<object>>(expr).Compile();
            Equal("aa", lambda());
        }
    }
}