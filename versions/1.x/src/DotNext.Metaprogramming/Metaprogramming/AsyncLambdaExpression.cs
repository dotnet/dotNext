using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;
    using Runtime.CompilerServices;
    using static Reflection.DelegateType;

    internal sealed class AsyncLambdaExpression<D> : LambdaExpression, ILexicalScope<Expression<D>, Action<LambdaContext>>
        where D : Delegate
    {
        private ParameterExpression recursion;
        private readonly TaskType taskType;

        [SuppressMessage("Usage", "CA2208", Justification = "The name of the generic parameter is correct")]
        internal AsyncLambdaExpression()
            : base(false)
        {
            if (typeof(D).IsAbstract)
                throw new GenericArgumentException<D>(ExceptionMessages.AbstractDelegate, nameof(D));
            var invokeMethod = GetInvokeMethod<D>();
            taskType = new TaskType(invokeMethod.ReturnType);
            Parameters = GetParameters(invokeMethod.GetParameters());
        }
        /// <summary>
        /// Gets this lambda expression suitable for recursive call.
        /// </summary>
        internal override Expression Self => recursion ?? (recursion = Expression.Variable(typeof(D), "self"));

        /// <summary>
        /// The list lambda function parameters.
        /// </summary>
        internal override IReadOnlyList<ParameterExpression> Parameters { get; }

        internal override Expression Return(Expression result) => new AsyncResultExpression(result, taskType);

        private new Expression<D> Build()
        {
            var body = base.Build();
            if (body.Type != taskType)
                body = body.AddEpilogue(true, new AsyncResultExpression(taskType));
            Expression<D> lambda;
            using (var builder = new AsyncStateMachineBuilder<D>(Parameters))
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

        public Expression<D> Build(Action<LambdaContext> scope)
        {
            using (var context = new LambdaContext(this))
                scope(context);
            return Build();
        }

        public override void Dispose()
        {
            recursion = null;
            base.Dispose();
        }
    }
}
