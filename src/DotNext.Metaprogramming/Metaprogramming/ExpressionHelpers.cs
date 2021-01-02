using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal static class ExpressionHelpers
    {
        [SuppressMessage("Usage", "CA1801", Justification = "Required by delegate signature")]
        internal static Expression TrivialCaseStatement(this Expression value, ParameterExpression parameter)
            => value;
    }
}
