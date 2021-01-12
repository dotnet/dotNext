using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using static Reflection.DelegateType;
    using Seq = Collections.Generic.Sequence;

    /// <summary>
    /// Represents lambda function builder.
    /// </summary>
    internal abstract class LambdaExpression : LexicalScope
    {
        private protected readonly bool tailCall;

        private protected LambdaExpression(bool tailCall)
            : base(false) => this.tailCall = tailCall;

        private protected static IReadOnlyList<ParameterExpression> GetParameters(System.Reflection.ParameterInfo[] parameters)
            => Array.ConvertAll(parameters, static parameter => Expression.Parameter(parameter.ParameterType, parameter.Name));

        /// <summary>
        /// Gets recursive reference to the lambda.
        /// </summary>
        /// <remarks>
        /// This property can be used to make recursive calls.
        /// </remarks>
        internal abstract Expression Self
        {
            get;
        }

        internal abstract ParameterExpression? Result { get; }

        /// <summary>
        /// Gets lambda parameters.
        /// </summary>
        internal abstract IReadOnlyList<ParameterExpression> Parameters { get; }

        internal abstract Expression Return(Expression? result);
    }

    /// <summary>
    /// Represents lambda function builder.
    /// </summary>
    /// <typeparam name="TDelegate">The delegate describing signature of lambda function.</typeparam>
    internal sealed class LambdaExpression<TDelegate> : LambdaExpression, ILexicalScope<Expression<TDelegate>, Action<LambdaContext>>, ILexicalScope<Expression<TDelegate>, Action<LambdaContext, ParameterExpression>>, ILexicalScope<Expression<TDelegate>, Func<LambdaContext, Expression>>
        where TDelegate : Delegate
    {
        private readonly Type returnType;
        private ParameterExpression? recursion;
        private ParameterExpression? lambdaResult;
        private LabelTarget? returnLabel;

        internal LambdaExpression(bool tailCall)
            : base(tailCall)
        {
            if (typeof(TDelegate).IsAbstract)
                throw new GenericArgumentException<TDelegate>(ExceptionMessages.AbstractDelegate, nameof(TDelegate));
            var invokeMethod = GetInvokeMethod<TDelegate>();
            Parameters = GetParameters(invokeMethod.GetParameters());
            returnType = invokeMethod.ReturnType;
        }

        /// <summary>
        /// Gets recursive reference to the lambda.
        /// </summary>
        /// <remarks>
        /// This property can be used to make recursive calls.
        /// </remarks>
        internal override Expression Self => recursion ?? (recursion = Expression.Variable(typeof(TDelegate), "self"));

        /// <summary>
        /// Gets lambda parameters.
        /// </summary>
        internal override IReadOnlyList<ParameterExpression> Parameters { get; }

        internal override ParameterExpression? Result
        {
            get
            {
                if (returnType == typeof(void))
                    return null;
                if (lambdaResult is null)
                    DeclareVariable(lambdaResult = Expression.Variable(returnType, "result"));
                return lambdaResult;
            }
        }

        internal override Expression Return(Expression? result)
        {
            if (returnLabel is null)
                returnLabel = Expression.Label("leave");
            if (result is null)
                result = Expression.Default(returnType);
            result = returnType == typeof(void) ? Expression.Return(returnLabel) : Expression.Block(Expression.Assign(Result!, result), Expression.Return(returnLabel));
            return result;
        }

        private new Expression<TDelegate> Build()
        {
            if (returnLabel is not null)
                AddStatement(Expression.Label(returnLabel));

            // last instruction should be always a result of a function
            if (lambdaResult is not null)
                AddStatement(lambdaResult);

            // rewrite body
            var body = Expression.Block(returnType, Variables, this);

            // build lambda expression
            if (recursion is not null)
            {
                body = Expression.Block(
                    Seq.Singleton(recursion),
                    Expression.Assign(recursion, Expression.Lambda<TDelegate>(body, tailCall, Parameters)),
                    Expression.Invoke(recursion, Parameters));
            }

            return Expression.Lambda<TDelegate>(body, tailCall, Parameters);
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

        public Expression<TDelegate> Build(Func<LambdaContext, Expression> body)
        {
            using (var context = new LambdaContext(this))
                AddStatement(body(context));
            return Build();
        }

        public override void Dispose()
        {
            lambdaResult = null;
            returnLabel = null;
            recursion = null;
            base.Dispose();
        }
    }
}