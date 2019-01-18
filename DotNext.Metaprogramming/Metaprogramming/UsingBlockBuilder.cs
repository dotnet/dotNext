using System;
using MethodInfo = System.Reflection.MethodInfo;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using static Reflection.Types;

    public sealed class UsingBlockBuilder: ExpressionBuilder, IExpressionBuilder<TryExpression>
    {
        private readonly MethodInfo disposeMethod;

        internal UsingBlockBuilder(Expression expression, ExpressionBuilder parent)
            : base(parent)
        {
            disposeMethod = expression.Type.GetDisposeMethod();
            if (disposeMethod is null)
                throw new ArgumentNullException($"Type {expression.Type.FullName} doesn't implement Dispose pattern");
            else if (expression is ParameterExpression variable)
                DisposableVar = variable;
            else
            {
                variable = DeclareVariable(expression.Type, NextName("disposable_"));
                parent.Assign(variable, expression);
                DisposableVar = variable;
            }
        }

        public UniversalExpression DisposableVar { get; }

        internal override Expression Build()
            => this.Upcast<IExpressionBuilder<TryExpression>, UsingBlockBuilder>().Build();
          
        TryExpression IExpressionBuilder<TryExpression>.Build()
            => Expression.TryFinally(base.Build(), DisposableVar.Call(disposeMethod));
    }
}
