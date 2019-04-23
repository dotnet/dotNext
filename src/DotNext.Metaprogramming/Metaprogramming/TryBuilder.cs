using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents structured exception handling statement.
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/try-catch">try-catch statement</seealso>
    public sealed class TryBuilder: ExpressionBuilder<TryExpression>
    {
        private readonly Expression tryBlock;
        private Expression faultBlock;
        private Expression finallyBlock;
        private readonly ICollection<CatchBlock> handlers;

        internal TryBuilder(ScopeBuilder builder, Expression tryBlock)
            : base(builder)
        {
            this.tryBlock = tryBlock;
            faultBlock = finallyBlock = null;
            handlers = new LinkedList<CatchBlock>();
        }

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <param name="exceptionType">Expected exception.</param>
        /// <param name="filter">Additional filter to be applied to the caught exception.</param>
        /// <param name="handler">Exception handling block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Catch(Type exceptionType, Func<ParameterExpression, Expression> filter, Action<ParameterExpression> handler)
        {
            VerifyCaller();
            var exception = Expression.Variable(exceptionType, "e");
            handlers.Add(Expression.MakeCatchBlock(exceptionType, exception, builder(() => handler(exception)), filter?.Invoke(exception)));
            return this;
        }

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <param name="exceptionType">Expected exception.</param>
        /// <param name="handler">Exception handling block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Catch(Type exceptionType, Action<ParameterExpression> handler) => Catch(exceptionType, null, handler);

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <typeparam name="E">Expected exception.</typeparam>
        /// <param name="handler">Exception handling block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Catch<E>(Action<ParameterExpression> handler) where E : Exception => Catch(typeof(E), handler);

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <param name="exceptionType">Expected exception.</param>
        /// <param name="handler">Exception handling block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Catch(Type exceptionType, Action handler)
        {
            VerifyCaller();
            handlers.Add(Expression.Catch(exceptionType, builder(handler)));
            return this;
        }

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <typeparam name="E">Expected exception.</typeparam>
        /// <param name="handler">Exception handling block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Catch<E>(Action handler) where E : Exception => Catch(typeof(E), handler);

        /// <summary>
        /// Constructs block of code which will be executed in case
        /// of any exception.
        /// </summary>
        /// <param name="fault">Fault handling block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Fault(Action fault) => Fault(builder(fault));

        /// <summary>
        /// Associates expression to be returned from structured exception handling block 
        /// in case of any exception.
        /// </summary>
        /// <param name="fault">The expression to be returned from SEH block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Fault(Expression fault)
        {
            VerifyCaller();
            faultBlock = fault;
            return this;
        }

        /// <summary>
        /// Constructs block of code run when control leaves a <see langword="try"/> statement.
        /// </summary>
        /// <param name="finally">The block of code to be executed.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Finally(Action @finally) => Finally(builder(@finally));

        /// <summary>
        /// Constructs single expression run when control leaves a <see langword="try"/> statement.
        /// </summary>
        /// <param name="finally">The single expression to be executed.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Finally(Expression @finally)
        {
            VerifyCaller();
            finallyBlock = @finally;
            return this;
        }

        private protected override TryExpression Build() => Expression.MakeTry(ExpressionType, tryBlock, finallyBlock, faultBlock, handlers);
    }
}