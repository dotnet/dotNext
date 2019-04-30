using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using LockExpression = Linq.Expressions.LockExpression;

    internal readonly struct LockStatement: IStatement<LockExpression, Action>, IStatement<LockExpression, Action<ParameterExpression>>
    {
        private readonly Expression syncRoot;

        internal LockStatement(Expression syncRoot) => this.syncRoot = syncRoot;

        LockExpression IStatement<LockExpression, Action>.Build(Action scope, ILexicalScope body)
        {
            scope();
            return new LockExpression(syncRoot, body.Build());
        }

        LockExpression IStatement<LockExpression, Action<ParameterExpression>>.Build(Action<ParameterExpression> scope, ILexicalScope body)
        {
            var result = new LockExpression(syncRoot);
            scope(result.SyncRoot);
            result.Body = body.Build();
            return result;
        }
    }
}