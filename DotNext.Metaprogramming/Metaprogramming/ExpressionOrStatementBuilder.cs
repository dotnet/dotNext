using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    public abstract class ExpressionOrStatementBuilder<E>: IExpressionBuilder<E>
        where E: Expression
    {
        private readonly ExpressionBuilder parent;
        private readonly bool treatAsStatement;
        private Type expressionType;

        private protected ExpressionOrStatementBuilder(ExpressionBuilder parent, bool treatAsStatement)
        {
            this.parent = parent;
            this.treatAsStatement = treatAsStatement;
        }

        private protected ExpressionBuilder NewScope() => new ExpressionBuilder(parent);

        private protected B NewScope<B>(Func<ExpressionBuilder, B> factory) => factory(parent);

        private protected Type ExpressionType
        {
            get => expressionType ?? typeof(void);
        }

        public ExpressionOrStatementBuilder<E> OfType(Type expressionType)
        {
            this.expressionType = expressionType;
            return this;
        }

        public ExpressionOrStatementBuilder<E> OfType<T>() => OfType(typeof(T));

        public E End()
        {
            var expr = Build();
            if(treatAsStatement)
                parent.AddStatement(expr);
            return expr;
        }

        private protected abstract E Build();

        E IExpressionBuilder<E>.Build() => Build();
    }
}