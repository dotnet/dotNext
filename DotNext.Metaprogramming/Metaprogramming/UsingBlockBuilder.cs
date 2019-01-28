using System;
using MethodInfo = System.Reflection.MethodInfo;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using static Reflection.Types;

    public sealed class UsingBlockBuilder: ScopeBuilder, IExpressionBuilder<TryExpression>
    {
        private readonly MethodInfo disposeMethod;
        private readonly ParameterExpression disposableVar;

        internal UsingBlockBuilder(Expression expression, ExpressionBuilder parent)
            : base(parent)
        {
            disposeMethod = expression.Type.GetDisposeMethod();
            if (disposeMethod is null)
                throw new ArgumentNullException($"Type {expression.Type.FullName} doesn't implement Dispose pattern");
            else if (expression is ParameterExpression variable)
                disposableVar = variable;
            else
            {
                disposableVar = DeclareVariable(expression.Type, NextName("disposable_"));
                Assign(disposableVar, expression);
            }
        }

        public UniversalExpression DisposableVar => disposableVar;

        internal override Expression Build()
            => this.Upcast<IExpressionBuilder<TryExpression>, UsingBlockBuilder>().Build();

        TryExpression IExpressionBuilder<TryExpression>.Build()
        {
            Expression @finally = disposableVar.Call(disposeMethod);
            @finally = Expression.Block(@finally, disposableVar.Assign(disposableVar.Type.Default()));
            return base.Build().Finally(@finally);
        }
    }
}
