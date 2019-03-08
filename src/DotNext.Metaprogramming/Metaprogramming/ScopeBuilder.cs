using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents lexical scope builder.
    /// </summary>
    public class ScopeBuilder : CompoundStatementBuilder
    {
        internal ScopeBuilder(CompoundStatementBuilder parent)
            : base(parent)
        {
        }

        internal Expression Build(Action<ScopeBuilder> body)
        {
            body(this);
            return Build();
        }

        /// <summary>
        /// Puts constant value as the result of this lexical scope.
        /// </summary>
        /// <typeparam name="T">The type of the constant.</typeparam>
        /// <param name="value">Constant value.</param>
        /// <returns>Constant expression added to this compound statement.</returns>
        public ConstantExpression Constant<T>(T value) => AddStatement(Expression.Constant(value, typeof(T)));

        /// <summary>
        /// Restarts execution of the loop.
        /// </summary>
        /// <param name="loop">The loop reference.</param>
        /// <returns>An expression representing jump to the beginning of the loop.</returns>
        public GotoExpression Continue(LoopBuilderBase loop)
            => AddStatement(loop.Continue(false));

        /// <summary>
        /// Stops the specified loop.
        /// </summary>
        /// <param name="loop">The loop reference.</param>
        /// <returns>An expression representing jump outside of the loop.</returns>
        public GotoExpression Break(LoopBuilderBase loop)
            => AddStatement(loop.Break(false));

        private LambdaBuilder FindLambda() => FindScope<LambdaBuilder>() ?? throw new InvalidOperationException(ExceptionMessages.CallFromLambdaExpected);

        /// <summary>
        /// Returns from underlying lambda expression without result.
        /// </summary>
        /// <remarks>
        /// Applicable for <see langword="void"/> lambda functions only.
        /// </remarks>
        /// <returns><see langword="return"/> statement.</returns>
        public sealed override Expression Return() => AddStatement(FindLambda().Return(false));

        /// <summary>
        /// Returns the given value from underlying lambda expression.
        /// </summary>
        /// <param name="result">The result to be returned from the lambda function.</param>
        /// <returns><see langword="return"/> statement.</returns>
        public sealed override Expression Return(UniversalExpression result) => AddStatement(FindLambda().Return(result, false));

        /// <summary>
        /// Adds re-throw expression into this scope if it or its parent is <see cref="CatchBuilder"/>.
        /// </summary>
        /// <returns>Re-throw exception.</returns>
        public Expression Rethrow() => AddStatement(Expression.Rethrow());
    }
}
