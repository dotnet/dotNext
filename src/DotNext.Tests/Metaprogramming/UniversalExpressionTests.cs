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
            dynamic left = new UniversalExpression(new int[] { 1, 2 }.AsConst<IList<int>>());
            left = left[0];
            UniversalExpression expr = left;
            expr.ToString();
        }
    }
}
