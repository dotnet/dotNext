using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

internal static class ExpressionHelpers
{
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313", Justification = "Underscore is used to indicate that the parameter is unused")]
    internal static Expression TrivialCaseStatement(this Expression value, ParameterExpression _)
        => value;
}