using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Metaprogramming
{
    using static Linq.Expressions.ExpressionBuilder;

    public sealed class RegressionIssue17 : Test
    {
        private static readonly PropertyInfo _propertyInfo = typeof(TestClass).GetProperties().First();

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task Regression(bool useCompilerGeneratedExpression)
        {
            var innerExp = GetTestExpression(useCompilerGeneratedExpression);

            var outerExp = CodeGenerator.AsyncLambda<Func<TestClass, Task<TestClass>>>(context =>
            {
                var output = innerExp.Invoke(context[0]).Await();
                CodeGenerator.Assign(output, _propertyInfo, Expression.Constant("updated", typeof(string)));
                CodeGenerator.Return(output);
            });

            var dlg = outerExp.Compile();
            var result = await dlg(new TestClass("original"));
        }

        private static Expression<Func<TestClass, Task<TestClass>>> GetTestExpression(bool useCompilerGeneratedExpression)
        {
            if (useCompilerGeneratedExpression)
            {
                return v => Task.FromResult(v);
            }

            return CodeGenerator.AsyncLambda<Func<TestClass, Task<TestClass>>>(context =>
            {
                CodeGenerator.Return(context[0]);
            });
        }

        public class TestClass
        {
            public TestClass(string testString)
            {
                TestString = testString;
            }

            public string TestString { get; set; }
        }
    }
}