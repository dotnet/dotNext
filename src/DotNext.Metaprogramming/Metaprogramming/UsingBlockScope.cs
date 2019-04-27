using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;

    internal sealed class UsingBlockScope : LexicalScope, IExpressionBuilder<UsingExpression>, ICompoundStatement<Action<ParameterExpression>>
    {
        private readonly UsingExpression expression;

        internal UsingBlockScope(Expression resource, LexicalScope parent = null) : base(parent) => expression = new UsingExpression(resource);
        
        public new UsingExpression Build() => new UsingExpression(expression) { Body = base.Build() };

        void ICompoundStatement<Action<ParameterExpression>>.ConstructBody(Action<ParameterExpression> body) => body(expression.Resource);
    }
}
