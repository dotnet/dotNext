using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices;

using static Collections.Generic.Collection;
using static Linq.Expressions.ExpressionBuilder;

/// <summary>
/// Represents statement.
/// </summary>
internal sealed class ClosureExpression : Expression
{
    private readonly LambdaExpression closure;
    private readonly IReadOnlyDictionary<ParameterExpression, ParameterExpression> mapping;

    internal ClosureExpression(LambdaExpression closure, IReadOnlyDictionary<ParameterExpression, ParameterExpression> mapping)
    {
        Debug.Assert(closure is not null);
        Debug.Assert(mapping is not null);

        this.closure = closure;
        this.mapping = mapping;
    }

    public override bool CanReduce => closure.CanReduce;

    public override ExpressionType NodeType => ExpressionType.Extension;

    public override Type Type => closure.Type;

    public override Expression Reduce() => closure.Reduce();

    internal BlockExpression Reduce(IReadOnlyDictionary<ParameterExpression, MemberExpression?> stateMachineContext)
    {
        ICollection<Expression> body = new LinkedList<Expression>();
        foreach (var (k, v) in mapping)
        {
            if (stateMachineContext[k]?.Expression is MemberExpression inner)
                body.Add(Expression.Assign(v, inner));
        }

        body.Add(closure);
        return Expression.Block(closure.Type, mapping.Values, body);
    }
}