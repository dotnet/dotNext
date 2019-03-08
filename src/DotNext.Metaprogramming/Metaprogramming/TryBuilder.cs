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

        internal TryBuilder(Expression tryBlock, CompoundStatementBuilder parent, bool treatAsStatement)
            : base(parent, treatAsStatement)
        {
            this.tryBlock = tryBlock;
            faultBlock = finallyBlock = null;
            handlers = new LinkedList<CatchBlock>();
        }

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <param name="exceptionType">Expected exception.</param>
        /// <param name="catch">Exception handling block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Catch(Type exceptionType, Action<CatchBuilder> @catch)
        {
            var catchBlock = NewScope(parent => new CatchBuilder(exceptionType, parent)).Build(@catch);
            handlers.Add(catchBlock);
            return this;
        }

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <param name="exceptionType">Expected exception.</param>
        /// <param name="filter">Additional filter to be applied to the caught exception.</param>
        /// <param name="body">Exception handling block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Catch(Type exceptionType, Func<ParameterExpression, UniversalExpression> filter, Func<ParameterExpression, UniversalExpression> body)
        {
            var exception = Expression.Variable(exceptionType, "e");
            handlers.Add(Expression.MakeCatchBlock(exceptionType, exception, body(exception), filter(exception)));
            return this;
        }

        /// <summary>
        /// Constructs exception handling section.
        /// </summary>
        /// <typeparam name="E">Expected exception.</typeparam>
        /// <param name="catch">Exception handling block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Catch<E>(Action<CatchBuilder> @catch)
            where E: Exception
            => Catch(typeof(E), @catch);

        /// <summary>
        /// Constructs block of code which will be executed in case
        /// of any exception.
        /// </summary>
        /// <param name="fault">Fault handling block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Fault(Action<CompoundStatementBuilder> fault)
            => Fault(NewScope().Build(fault));

        /// <summary>
        /// Associates expression to be returned from structured exception handling block 
        /// in case of any exception.
        /// </summary>
        /// <param name="fault">The expression to be returned from SEH block.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Fault(UniversalExpression fault)
        {
            faultBlock = fault;
            return this;
        }

        /// <summary>
        /// Constructs block of code run when control leaves a <see langword="try"/> statement.
        /// </summary>
        /// <param name="finally">The block of code to be executed.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Finally(Action<CompoundStatementBuilder> @finally)
            => Finally(NewScope().Build(@finally));

        /// <summary>
        /// Constructs single expression run when control leaves a <see langword="try"/> statement.
        /// </summary>
        /// <param name="finally">The single expression to be executed.</param>
        /// <returns><see langword="this"/> builder.</returns>
        public TryBuilder Finally(UniversalExpression @finally)
        {
            finallyBlock = @finally;
            return this;
        }

        private protected override TryExpression Build() => Expression.MakeTry(ExpressionType, tryBlock, finallyBlock, faultBlock, handlers);
    }
}