using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using LockExpression = Linq.Expressions.LockExpression;

    internal sealed class LockStatement : Statement, ILexicalScope<LockExpression, Action>, ILexicalScope<LockExpression, Action<ParameterExpression>>
    {
        private readonly Expression syncRoot;

        internal LockStatement(Expression syncRoot) => this.syncRoot = syncRoot;

        LockExpression ILexicalScope<LockExpression, Action>.Build(Action scope)
        {
            scope();
            return new LockExpression(syncRoot) { Body = Build() };
        }

        LockExpression ILexicalScope<LockExpression, Action<ParameterExpression>>.Build(Action<ParameterExpression> scope)
        {
            var result = new LockExpression(syncRoot);
            scope(result.SyncRoot);
            result.Body = Build();
            return result;
        }
    }
}