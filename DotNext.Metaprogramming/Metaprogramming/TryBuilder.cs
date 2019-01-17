using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    public sealed class TryBuilder: ExpressionOrStatementBuilder<TryExpression>
    {
        private readonly Expression tryBlock;
        private Expression faultBlock;
        private Expression finallyBlock;
        private readonly ICollection<CatchBlock> handlers;

        internal TryBuilder(Expression tryBlock, ExpressionBuilder parent, bool treatAsStatement)
            : base(parent, treatAsStatement)
        {
            this.tryBlock = tryBlock;
            faultBlock = finallyBlock = null;
            handlers = new LinkedList<CatchBlock>();
        }

        public TryBuilder Catch(Type exceptionType, Action<CatchBuilder> @catch)
        {
            var catchBlock = NewScope(parent => new CatchBuilder(exceptionType, parent)).Build(@catch);
            handlers.Add(catchBlock);
            return this;
        }

        public TryBuilder Catch<E>(Action<CatchBuilder> @catch)
            where E: Exception
            => Catch(typeof(E), @catch);

        public TryBuilder Fault(Action<ExpressionBuilder> fault)
        {
            faultBlock = NewScope().Build(fault);
            return this;
        }

        public TryBuilder Finally(Action<ExpressionBuilder> @finally)
        {
            finallyBlock = NewScope().Build(@finally);
            return this;
        }

        private protected override TryExpression Build() => Expression.MakeTry(ExpressionType, tryBlock, finallyBlock, faultBlock, handlers);
    }
}