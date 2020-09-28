using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Xunit;

namespace DotNext.Linq.Expressions
{
    [ExcludeFromCodeCoverage]
    [Obsolete("Set of tests for deprecated UniversalExpression type")]
    public sealed class UniversalExpressionTests : Test
    {
        private sealed class MyClass
        {
            public string Field;
        }

        [Fact]
        public static void UnaryOperator()
        {
            dynamic expr = (UniversalExpression)10;
            expr = -expr;
            Expression staticExpr = expr;
            IsAssignableFrom<UnaryExpression>(staticExpr);
        }

        [Fact]
        public static void BinaryOperator()
        {
            dynamic expr = (UniversalExpression)10;
            expr = expr + 20;
            Expression staticExpr = expr;
            IsAssignableFrom<BinaryExpression>(staticExpr);
        }

        [Fact]
        public static void Call()
        {
            dynamic expr = (UniversalExpression)"Hello, world";
            expr = expr.IndexOf('e');
            Expression staticExpr = expr;
            IsAssignableFrom<MethodCallExpression>(staticExpr);
        }

        [Fact]
        public static void Member()
        {
            dynamic expr = (UniversalExpression)"Hello, world";
            expr = expr.Length;
            Expression staticExpr = expr;
            IsAssignableFrom<MemberExpression>(staticExpr);
        }

        [Fact]
        public static void SetMember()
        {
            dynamic expr = (UniversalExpression)new MyClass() { Field = null }.Const();
            expr = expr.Field = "value";
            Expression staticExpr = expr;
            IsAssignableFrom<BinaryExpression>(staticExpr);
        }

        [Fact]
        public static void Indexer()
        {
            dynamic expr = (UniversalExpression)new[] { 1, 2 }.Const<IList<int>>();
            expr = expr[0];
            Expression staticExpr = expr;
            IsAssignableFrom<IndexExpression>(staticExpr);
        }

        [Fact]
        public static void Invoke()
        {
            dynamic expr = (UniversalExpression)new Action<string>(Console.WriteLine).Const();
            expr = expr("Hello, world");
            Expression staticExpr = expr;
            IsAssignableFrom<InvocationExpression>(staticExpr);
        }

        [Fact]
        public static void Concatenation()
        {
            UniversalExpression expr = "Hello";
            expr = expr.Concat(", ".Const(), "world!".Const());
            Equal(ExpressionType.Call, expr.NodeType);
        }
    }
}
