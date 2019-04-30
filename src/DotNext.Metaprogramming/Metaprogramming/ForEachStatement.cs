using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;

    internal sealed class ForEachLoopScope : LoopScopeBase, IExpressionBuilder<ForEachExpression>, ICompoundStatement<Action<MemberExpression, LoopContext>>, ICompoundStatement<Action<MemberExpression>>
    {
        private readonly ForEachExpression expression;

        internal ForEachLoopScope(Expression collection, LexicalScope parent) : base(parent) => expression = new ForEachExpression(collection);

        internal override LabelTarget ContinueLabel => expression.ContinueLabel;

        internal override LabelTarget BreakLabel => expression.BreakLabel;

        public new ForEachExpression Build() => expression.Update(base.Build());

        void ICompoundStatement<Action<MemberExpression, LoopContext>>.ConstructBody(Action<MemberExpression, LoopContext> body)
        {
            using (var cookie = new LoopContext(this))
                body(expression.Element, cookie);
        }

        void ICompoundStatement<Action<MemberExpression>>.ConstructBody(Action<MemberExpression> body) => body(expression.Element);
    }
}
