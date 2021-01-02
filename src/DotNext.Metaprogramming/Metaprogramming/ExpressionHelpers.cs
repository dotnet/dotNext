using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal static class ExpressionHelpers
    {
        internal static Expression TrivialCaseStatement(this Expression value, ParameterExpression _)
            => value;
    }
}
