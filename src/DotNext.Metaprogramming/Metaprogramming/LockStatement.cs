using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using LockExpression = Linq.Expressions.LockExpression;

    internal sealed class LockStatement: LexicalScope, ILexicalScope<LockExpression, Action>, ILexicalScope<LockExpression, Action<ParameterExpression>>
    {
        internal readonly struct Factory : IFactory<LockStatement>
        {
            private readonly Expression syncRoot;

            internal Factory(Expression syncRoot) => this.syncRoot = syncRoot;

            public LockStatement Create(LexicalScope parent) => new LockStatement(syncRoot, parent);
        }

        private readonly Expression syncRoot;

        private LockStatement(Expression syncRoot, LexicalScope parent) : base(parent) => this.syncRoot = syncRoot;

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