using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;
    using Runtime.CompilerServices;
    using static Reflection.DelegateType;
    using Seq = Collections.Generic.Sequence;

    internal sealed class AsyncLambdaExpression<TDelegate> : LambdaExpression, ILexicalScope<Expression<TDelegate>, Action<LambdaContext>>, ILexicalScope<Expression<TDelegate>, Action<LambdaContext, ParameterExpression>>
        where TDelegate : Delegate
    {
        private readonly TaskType taskType;
        private ParameterExpression? recursion;
        private ParameterExpression? lambdaResult;

        public AsyncLambdaExpression()
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
        internal override Expression Self => recursion ??= Expression.Variable(typeof(TDelegate), "self");

        internal override ParameterExpression? Result
        {
            get
            {
                if (taskType.ResultType == typeof(void))
                    return null;
                else if (lambdaResult is null)
                    DeclareVariable(lambdaResult = Expression.Variable(taskType.ResultType, "result"));
                return lambdaResult;
            }
        }

        /// <summary>
        /// The list lambda function parameters.
        /// </summary>
        internal override IReadOnlyList<ParameterExpression> Parameters { get; }

        internal override Expression Return(Expression? result)
        {
            result ??= lambdaResult;
            return new AsyncResultExpression(result, taskType);
        }

        private new Expression<TDelegate> Build()
        {
            var body = base.Build();
            if (lambdaResult is not null)
                body = body.AddEpilogue(taskType.HasResult, new AsyncResultExpression(lambdaResult, taskType));
            else if (body.Type != taskType)
                body = body.AddEpilogue(taskType.HasResult, new AsyncResultExpression(taskType));
            Expression<TDelegate> lambda;
            using (var builder = new AsyncStateMachineBuilder<TDelegate>(Parameters))
            {
                lambda = builder.Build(body, tailCall);
            }

            // build lambda expression
            if (recursion is not null)
            {
                lambda = Expression.Lambda<TDelegate>(
                    Expression.Block(
                    Seq.Singleton(recursion),
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

        public Expression<TDelegate> Build(Action<LambdaContext, ParameterExpression> scope)
        {
            using (var context = new LambdaContext(this))
                scope(context, Result ?? throw new InvalidOperationException(ExceptionMessages.VoidLambda));
            return Build();
        }

        public override void Dispose()
        {
            recursion = null;
            base.Dispose();
        }
    }
}
