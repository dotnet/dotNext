using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Runtime.CompilerServices;
    using static Reflection.DelegateType;

    /// <summary>
    /// Provides asynchronous lambda expression with await support.
    /// </summary>
    /// <remarks>
    /// Delegate should returns <see cref="Task"/> or <see cref="Task{TResult}"/>.
    /// </remarks>
    /// <typeparam name="D">Type of delegate representing lambda signature.</typeparam>
    /// <see cref="Task"/>
    /// <see cref="Task{TResult}"/>
    public sealed class AsyncLambdaBuilder<D>: LambdaBuilder, IExpressionBuilder<Expression<D>>
        where D: Delegate
    {
        private ParameterExpression recursion;
        private readonly TaskType taskType;

        internal AsyncLambdaBuilder(CompoundStatementBuilder parent = null)
            : base(parent)
        {
            if (typeof(D).IsAbstract)
                throw new GenericArgumentException<D>(ExceptionMessages.AbstractDelegate, nameof(D));
            var invokeMethod = GetInvokeMethod<D>();
            taskType = new TaskType(invokeMethod.ReturnType);
            Parameters = GetParameters(invokeMethod.GetParameters());
        }

        /// <summary>
        /// Sets the body of lambda expression.
        /// </summary>
        public override Expression Body { set => base.Body = new AsyncResultExpression(value, taskType); }

        /// <summary>
        /// Gets this lambda expression suitable for recursive call.
        /// </summary>
        public override Expression Self
        {
            get
            {
                if (recursion is null)
                    recursion = Expression.Variable(typeof(D), "self");
                return recursion;
            }
        }

        /// <summary>
        /// Return type of lambda function.
        /// </summary>
        public override Type ReturnType => taskType;

        /// <summary>
        /// The list lambda function parameters.
        /// </summary>
        public override IReadOnlyList<ParameterExpression> Parameters { get; }

        internal override Expression Return(Expression result, bool addAsStatement)
        {
            var asyncResult = new AsyncResultExpression(result, taskType);
            return addAsStatement ? AddStatement(asyncResult) : asyncResult;
        }

        private protected override LambdaExpression Build(Expression body, bool tailCall)
        {
            if (body.Type != taskType)
                body = body.AddEpilogue(true, new AsyncResultExpression(taskType));
            Expression<D> lambda;
            using(var builder = new Runtime.CompilerServices.AsyncStateMachineBuilder<D>(Parameters))
            {
                lambda = builder.Build(body, tailCall);
            }
            //build lambda expression
            if (!(recursion is null))
                lambda = Expression.Lambda<D>(Expression.Block(Sequence.Singleton(recursion),
                    Expression.Assign(recursion, lambda),
                    Expression.Invoke(recursion, Parameters)), Parameters);
            return lambda;
        }

        Expression<D> IExpressionBuilder<Expression<D>>.Build() => (Expression<D>)Build();

        /// <summary>
        /// Releases all resources associated with this builder.
        /// </summary>
        /// <param name="disposing"><see langword="true"/>, if this method is called from <see cref="Disposable.Dispose()"/>; <see langword="false"/> if called from finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                recursion = null;
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Constructs async lambda function.
        /// </summary>
        /// <param name="tailCall"><see langword="true"/> if the lambda expression will be compiled with the tail call optimization, otherwise <see langword="false"/>.</param>
        /// <param name="lambdaBody">Lambda function body with <see langword="await"/> expressions.</param>
        /// <returns>Asynchronous lambda function.</returns>
        public static Expression<D> Build(bool tailCall, Action<AsyncLambdaBuilder<D>> lambdaBody)
        {
            var builder = new AsyncLambdaBuilder<D>() { TailCall = tailCall };
            lambdaBody(builder);
            return ((IExpressionBuilder<Expression<D>>)builder).Build();
        }

        /// <summary>
        /// Constructs async lambda expression from expression tree.
        /// </summary>
        /// <param name="lambdaBody">Lambda expression builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        public static Expression<D> Build(Action<AsyncLambdaBuilder<D>> lambdaBody)
            => Build(false, lambdaBody);
    }
}
