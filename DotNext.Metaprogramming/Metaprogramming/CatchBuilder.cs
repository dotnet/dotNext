using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    public sealed class CatchBuilder: ExpressionBuilder
    {
        private Expression filter;

        internal CatchBuilder(Type exceptionType, ExpressionBuilder parent)
            : base(parent)
        {
            Exception = Expression.Variable(exceptionType, "e");
        }

        /// <summary>
        /// Represents captured exception.
        /// </summary>
        public ParameterExpression Exception { get; }

        public void Filter(Action<ExpressionBuilder> filter)
            => this.filter = new ExpressionBuilder(Parent).Build(filter);

        internal CatchBlock Build(Action<CatchBuilder> body)
        {
            body(this);
            return Expression.MakeCatchBlock(Exception.Type, Exception, Build(), filter);
        }
    }
}