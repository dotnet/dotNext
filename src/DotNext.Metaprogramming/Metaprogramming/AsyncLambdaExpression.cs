using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;
    using Runtime.CompilerServices;
    using static Reflection.DelegateType;

    internal sealed class AsyncLambdaExpression<TDelegate> : LambdaExpression, ILexicalScope<Expression<TDelegate>, Action<LambdaContext>>
        where TDelegate : Delegate
    {
        private readonly TaskType taskType;
        private ParameterExpression? recursion;

        [SuppressMessage("Usage", "CA2208", Justification = "The name of the generic parameter is correct")]
        internal AsyncLambdaExpression()
            : base(false)
        {
            if (typeof(TDelegate).IsAbstract)
                throw new GenericArgumentException<TDelegate>(ExceptionMessages.AbstractDelegate, nameof(TDelegate));
            var invokeMethod = GetInvokeMethod<TDelegate>();
            taskType = new TaskType(invokeMethod.ReturnType);
            Parameters = GetParameters(invokeMethod.GetParameters());
        }

        /// <summary>
        /// Gets this lambda expression suitable for recursive call.
        /// </summary>
        internal override Expression Self => recursion ?? (recursion = Expression.Variable(typeof(TDelegate), "self"));

        /// <summary>
        /// The list lambda function parameters.
        /// </summary>
        internal override IReadOnlyList<ParameterExpression> Parameters { get; }

        internal override Expression Return(Expression? result) => new AsyncResultExpression(result, taskType);

        private new Expression<TDelegate> Build()
        {
            var body = base.Build();
            if (body.Type != taskType)
                body = body.AddEpilogue(taskType.HasResult, new AsyncResultExpression(taskType));
            Expression<TDelegate> lambda;
            using (var builder = new AsyncStateMachineBuilder<TDelegate>(Parameters))
            {
                lambda = builder.Build(body, tailCall);
            }

            // build lambda expression
            if (!(recursion is null))
            {
                lambda = Expression.Lambda<TDelegate>(
                    Expression.Block(
                    Sequence.Singleton(recursion),
                    Expression.Assign(recursion, lambda),
                    Expression.Invoke(recursion, Parameters)), Parameters);
            }

            return lambda;
        }

        public Expression<TDelegate> Build(Action<LambdaContext> scope)
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
