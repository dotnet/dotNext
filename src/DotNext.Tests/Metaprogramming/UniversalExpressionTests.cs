using System;
using System.Collections.Generic;
using Xunit;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    public sealed class UniversalExpressionTests: Assert
    {
        private sealed class MyClass
        {
            public string Field;
        }

        [Fact]
        public void UnaryOperatorTest()
        {
            dynamic expr = (UniversalExpression)10;
            expr = -expr;
            Expression staticExpr = expr;
            IsAssignableFrom<UnaryExpression>(staticExpr);
        }

        [Fact]
        public void BinaryOperatorTest()
        {
            dynamic expr = (UniversalExpression)10;
            expr = expr + 20;
            Expression staticExpr = expr;
            IsAssignableFrom<BinaryExpression>(staticExpr);
        }

        [Fact]
        public void CallTest()
        {
            dynamic expr = (UniversalExpression)"Hello, world";
            expr = expr.IndexOf('e');
            Expression staticExpr = expr;
            IsAssignableFrom<MethodCallExpression>(staticExpr);
        }

        [Fact]
        public void MemberTest()
        {
            dynamic expr = (UniversalExpression)"Hello, world";
            expr = expr.Length;
            Expression staticExpr = expr;
            IsAssignableFrom<MemberExpression>(staticExpr);
        }

        [Fact]
        public void SetMemberTest()
        {
            dynamic expr = new UniversalExpression(new MyClass().AsConst());
            expr = expr.Field = "value";
            Expression staticExpr = expr;
            IsAssignableFrom<BinaryExpression>(staticExpr);
        }

        [Fact]
        public void IndexerTest()
        {
            dynamic expr = new UniversalExpression(new[] { 1, 2 }.AsConst<IList<int>>());
            expr = expr[0];
            Expression staticExpr = expr;
            IsAssignableFrom<IndexExpression>(staticExpr);
        }

        [Fact]
        public void InvokeTest()
        {
            dynamic expr = new UniversalExpression(new Action<string>(Console.WriteLine).AsConst());
            expr = expr("Hello, world");
            Expression staticExpr = expr;
            IsAssignableFrom<InvocationExpression>(staticExpr);
        }

        [Fact]
        public void NewExpression()
        {
            var lambda = LambdaBuilder<Func<Type, char[], object>>.Build(fun =>
            {
                fun.Body = fun.Parameters[0].New(fun.Parameters[1]);
            }).Compile();
            var str = lambda(typeof(string), new char[] { 'a', 'b' });
            Equal("ab", str);
        }
    }
}
