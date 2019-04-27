using System;
using System.Linq.Expressions;
using static System.Threading.Thread;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents compound expresssion builder.
    /// </summary>
    /// <remarks>
    /// Any derived expression builder is not thread-safe and event cannot
    /// be shared between threads.
    /// </remarks>
    /// <typeparam name="E">Type of expression to be constructed.</typeparam>
    public abstract class ExpressionBuilder<E> : IExpressionBuilder<E>
        where E : Expression
    {
        internal delegate Expression ScopeBuilder(Action body);

        private protected readonly ScopeBuilder builder;
        private Type expressionType;
        private readonly int ownerThread;

        private protected ExpressionBuilder(ScopeBuilder builder)
        {
            this.builder = builder;
            ownerThread = CurrentThread.ManagedThreadId;
        }

        private protected void VerifyCaller()
        {
            if (ownerThread != CurrentThread.ManagedThreadId)
                throw new InvalidOperationException();
        }

        internal event Action<E> Constructed;

        private protected Type ExpressionType => expressionType ?? typeof(void);

        /// <summary>
        /// Changes type of the expression.
        /// </summary>
        /// <remarks>
        /// By default, type of expression is <see cref="void"/>.
        /// </remarks>
        /// <param name="expressionType">The expression type.</param>
        /// <returns>This builder.</returns>
        public ExpressionBuilder<E> OfType(Type expressionType)
        {
            VerifyCaller();
            this.expressionType = expressionType;
            return this;
        }

        /// <summary>
        /// Changes type of the expression.
        /// </summary>
        /// <typeparam name="T">The expression type.</typeparam>
        /// <returns>This builder.</returns>
        public ExpressionBuilder<E> OfType<T>() => OfType(typeof(T));

        /// <summary>
        /// Constructs expression and, optionally, adds it to the underlying compound statement.
        /// </summary>
        /// <returns>Constructed expression.</returns>
        public E End()
        {
            VerifyCaller();
            var expr = Build();
            Constructed?.Invoke(expr);
            return expr;
        }

        private protected abstract E Build();

        E IExpressionBuilder<E>.Build() => Build();
    }
}