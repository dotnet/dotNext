using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

using static Linq.Expressions.ExpressionBuilder;

public sealed class RegressionIssue17 : Test
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task Regression(bool useCompilerGeneratedExpression)
    {
        var propertyInfo = typeof(TestClass).GetProperty(nameof(TestClass.TestString));
        NotNull(propertyInfo);
        var innerExp = GetTestExpression(useCompilerGeneratedExpression);

        var outerExp = CodeGenerator.AsyncLambda<Func<TestClass, Task<TestClass>>>(context =>
        {
            var output = innerExp.Invoke(context[0]).Await();
            CodeGenerator.Assign(output, propertyInfo, Expression.Constant("updated", typeof(string)));
            CodeGenerator.Return(output);
        });

        var dlg = outerExp.Compile();
        var result = await dlg(new TestClass("original"));
        Equal("updated", result.TestString);
    }

    private static Expression<Func<TestClass, Task<TestClass>>> GetTestExpression(bool useCompilerGeneratedExpression)
    {
        if (useCompilerGeneratedExpression)
        {
            return static v => Task.FromResult(v);
        }

        return CodeGenerator.AsyncLambda<Func<TestClass, Task<TestClass>>>(context =>
        {
            CodeGenerator.Return(context[0]);
        });
    }

    public class TestClass(string testString)
    {
        public string TestString { get; set; } = testString;
    }
}