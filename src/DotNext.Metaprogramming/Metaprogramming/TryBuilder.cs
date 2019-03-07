using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    public sealed class TryBuilder: ExpressionBuilder<TryExpression>
    {
        private readonly Expression tryBlock;
        private Expression faultBlock;
        private Expression finallyBlock;
        private readonly ICollection<CatchBlock> handlers;

        internal TryBuilder(Expression tryBlock, CompoundStatementBuilder parent, bool treatAsStatement)
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

        public TryBuilder Catch(Type exceptionType, Func<ParameterExpression, UniversalExpression> filter, Func<ParameterExpression, UniversalExpression> body)
        {
            var exception = Expression.Variable(exceptionType, "e");
            handlers.Add(Expression.MakeCatchBlock(exceptionType, exception, body(exception), filter(exception)));
            return this;
        }

        public TryBuilder Catch<E>(Action<CatchBuilder> @catch)
            where E: Exception
            => Catch(typeof(E), @catch);

        public TryBuilder Fault(Action<CompoundStatementBuilder> fault)
            => Fault(NewScope().Build(fault));

        public TryBuilder Fault(UniversalExpression fault)
        {
            faultBlock = fault;
            return this;
        }

        public TryBuilder Finally(Action<CompoundStatementBuilder> @finally)
            => Finally(NewScope().Build(@finally));

        public TryBuilder Finally(UniversalExpression @finally)
        {
            finallyBlock = @finally;
            return this;
        }

        private protected override TryExpression Build() => Expression.MakeTry(ExpressionType, tryBlock, finallyBlock, faultBlock, handlers);
    }
}