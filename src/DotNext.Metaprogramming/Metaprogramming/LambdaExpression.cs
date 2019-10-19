using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using static Reflection.DelegateType;

    /// <summary>
    /// Represents lambda function builder.
    /// </summary>
    internal abstract class LambdaExpression : LexicalScope
    {
        private protected readonly bool tailCall;

        private protected LambdaExpression(bool tailCall) : base(false) => this.tailCall = tailCall;

        private protected IReadOnlyList<ParameterExpression> GetParameters(System.Reflection.ParameterInfo[] parameters)
            => Array.ConvertAll(parameters, parameter => Expression.Parameter(parameter.ParameterType, parameter.Name));

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

        /// <summary>
        /// Gets lambda parameters.
        /// </summary>
        internal abstract IReadOnlyList<ParameterExpression> Parameters { get; }

        internal abstract Expression Return(Expression result);
    }

    /// <summary>
    /// Represents lambda function builder.
    /// </summary>
    /// <typeparam name="D">The delegate describing signature of lambda function.</typeparam>
    internal sealed class LambdaExpression<D> : LambdaExpression, ILexicalScope<Expression<D>, Action<LambdaContext>>, ILexicalScope<Expression<D>, Action<LambdaContext, ParameterExpression>>, ILexicalScope<Expression<D>, Func<LambdaContext, Expression>>
        where D : Delegate
    {
        private ParameterExpression recursion;
        private ParameterExpression lambdaResult;
        private LabelTarget returnLabel;

        private readonly Type returnType;

        [SuppressMessage("Usage", "CA2208", Justification = "The name of the generic parameter is correct")]
        internal LambdaExpression(bool tailCall)
            : base(tailCall)
        {
            if (typeof(D).IsAbstract)
                throw new GenericArgumentException<D>(ExceptionMessages.AbstractDelegate, nameof(D));
            var invokeMethod = GetInvokeMethod<D>();
            Parameters = GetParameters(invokeMethod.GetParameters());
            returnType = invokeMethod.ReturnType;
        }

        /// <summary>
        /// Gets recursive reference to the lambda.
        /// </summary>
        /// <remarks>
        /// This property can be used to make recursive calls.
        /// </remarks>
        internal override Expression Self => recursion ?? (recursion = Expression.Variable(typeof(D), "self"));

        /// <summary>
        /// Gets lambda parameters.
        /// </summary>
        internal override IReadOnlyList<ParameterExpression> Parameters { get; }

        private ParameterExpression Result
        {
            get
            {
                if (returnType == typeof(void))
                    return null;
                else if (lambdaResult is null)
                    DeclareVariable(lambdaResult = Expression.Variable(returnType, "result"));
                return lambdaResult;
            }
        }

        internal override Expression Return(Expression result)
        {
            if (returnLabel is null)
                returnLabel = Expression.Label("leave");
            if (result is null)
                result = Expression.Default(returnType);
            result = returnType == typeof(void) ? (Expression)Expression.Return(returnLabel) : Expression.Block(Expression.Assign(Result, result), Expression.Return(returnLabel));
            return result;
        }

        private new Expression<D> Build()
        {
            if (!(returnLabel is null))
                AddStatement(Expression.Label(returnLabel));
            //last instruction should be always a result of a function
            if (!(lambdaResult is null))
                AddStatement(lambdaResult);
            //rewrite body
            var body = Expression.Block(returnType, Variables, this);
            //build lambda expression
            if (!(recursion is null))
                body = Expression.Block(Sequence.Singleton(recursion),
                    Expression.Assign(recursion, Expression.Lambda<D>(body, tailCall, Parameters)),
                    Expression.Invoke(recursion, Parameters));
            return Expression.Lambda<D>(body, tailCall, Parameters);
        }

        public Expression<D> Build(Action<LambdaContext> scope)
        {
            using (var context = new LambdaContext(this))
                scope(context);
            return Build();
        }

        public Expression<D> Build(Action<LambdaContext, ParameterExpression> scope)
        {
            using (var context = new LambdaContext(this))
                scope(context, Result);
            return Build();
        }

        public Expression<D> Build(Func<LambdaContext, Expression> body)
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