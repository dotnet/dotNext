using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents lexical scope builder.
    /// </summary>
    public class ScopeBuilder : ExpressionBuilder
    {
        internal ScopeBuilder(ExpressionBuilder parent)
            : base(parent)
        {
        }

        internal Expression Build(Action<ScopeBuilder> body)
        {
            body(this);
            return Build();
        }

        public ConstantExpression Constant<T>(T value) => AddStatement(Expression.Constant(value, typeof(T)));

        public GotoExpression Continue(LoopBuilderBase loop)
            => AddStatement(loop.Continue(false));

        /// <summary>
        /// Stops the specified loop.
        /// </summary>
        /// <param name="loop">Loop identifier.</param>
        /// <returns>An expression representing jumping outside of the loop.</returns>
        public GotoExpression Break(LoopBuilderBase loop)
            => AddStatement(loop.Break(false));

        private LambdaBuilder FindLambda() => FindScope<LambdaBuilder>() ?? throw new InvalidOperationException(ExceptionMessages.CallFromLambdaExpected);

        /// <summary>
        /// Returns from underlying lambda expression.
        /// </summary>
        /// <param name="lambda"></param>
        /// <returns></returns>
        public sealed override Expression Return() => AddStatement(FindLambda().Return(false));

        public sealed override Expression Return(UniversalExpression result) => AddStatement(FindLambda().Return(result, false));

        public Expression Rethrow() => AddStatement(Expression.Rethrow());
    }
}
