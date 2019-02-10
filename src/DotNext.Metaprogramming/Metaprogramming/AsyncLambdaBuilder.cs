using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using static Reflection.Types;

    /// <summary>
    /// Provides asynchronous lambda expression with await support.
    /// </summary>
    /// <remarks>
    /// Delegate should returns <see cref="Task"/> or <see cref="Task{TResult}"/>.
    /// </remarks>
    /// <typeparam name="D">Type of delegate representing lambda signature.</typeparam>
    /// <see cref="Tast"/>
    /// <see cref="Task{TResult}"/>
    public sealed class AsyncLambdaBuilder<D>: LambdaBuilder, IExpressionBuilder<Expression<D>>
        where D: Delegate
    {
        private ParameterExpression recursion;
        private readonly Type taskType;

        internal AsyncLambdaBuilder(ExpressionBuilder parent = null)
            : base(parent)
        {
            if (typeof(D).IsAbstract)
                throw new GenericArgumentException<D>(ExceptionMessages.AbstractDelegate, nameof(D));
            var invokeMethod = Delegates.GetInvokeMethod<D>();
            taskType = invokeMethod.ReturnType;
            Parameters = GetParameters(invokeMethod.GetParameters());
        }

        public override Expression Body { set => base.Body = new AsyncResultExpression(value); }

        public override Expression Self
        {
            get
            {
                if (recursion is null)
                    recursion = Expression.Variable(typeof(D), "self");
                return recursion;
            }
        }

        public override Type ReturnType => taskType.GetTaskType() ?? throw new GenericArgumentException<D>(ExceptionMessages.TaskTypeExpected, nameof(D));

        public override IReadOnlyList<ParameterExpression> Parameters { get; }

        internal override Expression Return(Expression result, bool addAsStatement)
        {
            var asyncResult = new AsyncResultExpression(result);
            return addAsStatement ? AddStatement(asyncResult) : asyncResult;
        }

        private protected override LambdaExpression Build(Expression body, bool tailCall)
        {
            if (body.Type != taskType)
            {
                var defaultResult = taskType == typeof(Task) ? new AsyncResultExpression() : new AsyncResultExpression(ReturnType.AsDefault());
                body = body.AddEpilogue(true, defaultResult);
            }
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                recursion = null;
            }
            base.Dispose(disposing);
        }

        public static Expression<D> Build(bool tailCall, Action<AsyncLambdaBuilder<D>> lambdaBody)
        {
            var builder = new AsyncLambdaBuilder<D>() { TailCall = tailCall };
            lambdaBody(builder);
            return builder.Upcast<IExpressionBuilder<Expression<D>>, AsyncLambdaBuilder<D>>().Build();
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
