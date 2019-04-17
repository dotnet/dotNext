using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents CATCH block builder as part of try-catch-finally statement.
    /// </summary>
    public sealed class CatchBuilder: ScopeBuilder
    {
        private Expression filter;

        internal CatchBuilder(Type exceptionType, CompoundStatementBuilder parent)
            : base(parent)
        {
            Exception = Expression.Variable(exceptionType, "e");
        }

        /// <summary>
        /// Represents captured exception.
        /// </summary>
        public ParameterExpression Exception { get; }

        /// <summary>
        /// Appends optional exception filter.
        /// </summary>
        /// <remarks>
        /// The exception handling filter is equal to <see langword="where"/> keyword applied
        /// inside of <see langword="catch"/> block in C#.
        /// Filter expression cannot have <see langword="await"/> expressions.
        /// </remarks>
        /// <param name="filter">Filter expression builder.</param>
        public void Filter(Action<CompoundStatementBuilder> filter)
        {
            using (var filterScope = new ScopeBuilder(Parent))
                this.filter = filterScope.Build(filter);
        }

        internal CatchBlock Build(Action<CatchBuilder> body)
        {
            body(this);
            return Expression.MakeCatchBlock(Exception.Type, Exception, Build(), filter);
        }
    }
}