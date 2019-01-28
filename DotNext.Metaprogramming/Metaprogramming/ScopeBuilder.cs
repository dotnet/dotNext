using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents lexical scope builder.
    /// </summary>
    public class ScopeBuilder: ExpressionBuilder
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

        public Expression Return(LambdaBuilder lambda) => AddStatement(lambda.Return(false));

        public Expression Return(LambdaBuilder lambda, UniversalExpression result) => AddStatement(lambda.Return(result, false));

    }
}
