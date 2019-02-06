using System;
using MethodInfo = System.Reflection.MethodInfo;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using static Reflection.Types;

    public sealed class UsingBlockBuilder: ScopeBuilder, IExpressionBuilder<Expression>
    {
        private readonly MethodInfo disposeMethod;
        private readonly ParameterExpression disposableVar;
        private readonly BinaryExpression assignment;

        internal UsingBlockBuilder(Expression expression, ExpressionBuilder parent)
            : base(parent)
        {
            disposeMethod = expression.Type.GetDisposeMethod();
            if (disposeMethod is null)
                throw new ArgumentNullException(ExceptionMessages.DisposePatternExpected(expression.Type));
            else if (expression is ParameterExpression variable)
                disposableVar = variable;
            else
            {
                disposableVar = Expression.Variable(expression.Type, NextName("disposable_"));
                assignment = Expression.Assign(disposableVar, expression);
            }
        }

        public UniversalExpression DisposableVar => disposableVar;

        internal override Expression Build()
        {
            Expression @finally = disposableVar.Call(disposeMethod);
            @finally = Expression.Block(typeof(void), @finally, disposableVar.AssignDefault());
            @finally = base.Build().Finally(@finally);
            return assignment is null ?
                @finally :
                Expression.Block(typeof(void), Sequence.Single(disposableVar), Sequence.Single(assignment).Concat(Sequence.Single(@finally)));
        }

        Expression IExpressionBuilder<Expression>.Build() => Build();
    }
}
