using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal abstract class Statement : LexicalScope
    {
        private protected Statement(LexicalScope parent)
            : base(parent ?? throw new InvalidOperationException(ExceptionMessages.OutOfLexicalScope))
        {
        }
    }

    internal abstract class Statement<E> : LexicalScope, ILexicalScope<E, Action>
        where E : class
    {
        private protected Statement(LexicalScope parent)
            : base(parent)
        {
        }

        private protected abstract E CreateExpression(Expression body);

        E ILexicalScope<E, Action>.Build(Action scope)
        {
            scope();
            return CreateExpression(Build());
        }
    }
}