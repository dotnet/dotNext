using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using static Collections.Generic.Collection;

    /// <summary>
    /// Represents lambda expression builder.
    /// </summary>
    public abstract class LambdaBuilder: ExpressionBuilder
    {
        private protected LambdaBuilder(ExpressionBuilder parent = null)
            : base(parent)
        {
        }

        private protected IReadOnlyList<ParameterExpression> GetParameters(System.Reflection.ParameterInfo[] parameters)
            =>  OneDimensionalArray.Convert(parameters, parameter => Expression.Parameter(parameter.ParameterType, parameter.Name));

        /// <summary>
        /// Gets recursive reference to the lambda.
        /// </summary>
        /// <remarks>
        /// This property can be used to make recursive calls.
        /// </remarks>
        public abstract Expression Self
        {
            get;
        }

        /// <summary>
        /// Gets return type of the lambda function.
        /// </summary>
        public abstract Type ReturnType { get; }

        /// <summary>
        /// Gets lambda parameters.
        /// </summary>
        public abstract IReadOnlyList<ParameterExpression> Parameters { get; }

        private protected abstract LambdaExpression Build(Expression body, bool tailCall);

        internal sealed override Expression Build() => Build(base.Build(), TailCall);

        internal abstract Expression Return(Expression result, bool addAsStatement);

        internal Expression Return(bool addAsStatement) => Return(ReturnType.AsDefault(), addAsStatement);

        public sealed override Expression Return(UniversalExpression result) => Return(result, true);

        public sealed override Expression Return() => Return(true);

        private protected bool TailCall { private get; set; }
       
    }

    public sealed class LambdaBuilder<D>: LambdaBuilder, IExpressionBuilder<Expression<D>>
        where D: Delegate
    {
        private ParameterExpression recursion;
        private ParameterExpression lambdaResult;
        private LabelTarget returnLabel;

        internal LambdaBuilder(ExpressionBuilder parent = null)
            : base(parent)
        {
            if (typeof(D).IsAbstract)
                throw new GenericArgumentException<D>(ExceptionMessages.AbstractDelegate, nameof(D));
            var invokeMethod = Delegates.GetInvokeMethod<D>();
            Parameters = GetParameters(invokeMethod.GetParameters());
            ReturnType = invokeMethod.ReturnType;
        }

        /// <summary>
        /// Gets recursive reference to the lambda.
        /// </summary>
        /// <remarks>
        /// This property can be used to make recursive calls.
        /// </remarks>
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
        /// Gets lambda parameters.
        /// </summary>
        public override IReadOnlyList<ParameterExpression> Parameters { get; }

        /// <summary>
        /// Gets return type of the lambda function.
        /// </summary>
        public override Type ReturnType { get; }

        /// <summary>
        /// Gets lambda function result holder.
        /// </summary>
        public ParameterExpression Result
        {
            get
            {
                if (ReturnType == typeof(void))
                    return null;
                else if (lambdaResult is null)
                    lambdaResult = DeclareVariable(ReturnType, NextName("lambdaResult_"));
                return lambdaResult;
            }
        }

        /// <summary>
        /// Sets body of lambda expression as single expression.
        /// </summary>
        public sealed override Expression Body
        {
            set
            {
                returnLabel = null;
                base.Body = value;
            }
        }

        internal override Expression Return(Expression result, bool addAsStatement)
        {
            if (returnLabel is null)
                returnLabel = Expression.Label("leave");
            result = ReturnType == typeof(void) ? returnLabel.Return().Upcast<Expression, GotoExpression>() : Expression.Block(Expression.Assign(Result, result), returnLabel.Return());
            return addAsStatement ? AddStatement(result) : result;
        }

        private protected override LambdaExpression Build(Expression body, bool tailCall)
        {
            var instructions = new LinkedList<Expression>();
            IEnumerable<ParameterExpression> locals;
            if (body is BlockExpression block)
            {
                instructions.AddAll(block.Expressions);
                locals = block.Variables;
            }
            else
            {
                instructions.AddLast(body);
                locals = Array.Empty<ParameterExpression>();
            }
            if (!(returnLabel is null))
                instructions.AddLast(returnLabel.LandingSite());
            //last instruction should be always a result of a function
            if (!(lambdaResult is null))
                instructions.AddLast(lambdaResult);
            //rewrite body
            body = Expression.Block(locals, instructions);
            //build lambda expression
            if (!(recursion is null))
                body = Expression.Block(Sequence.Singleton(recursion), 
                    Expression.Assign(recursion, Expression.Lambda<D>(body, tailCall, Parameters)), 
                    Expression.Invoke(recursion, Parameters));
            return Expression.Lambda<D>(body, tailCall, Parameters);
        }

        Expression<D> IExpressionBuilder<Expression<D>>.Build() => (Expression<D>)Build();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lambdaResult = null;
                returnLabel = null;
                recursion = null;
            }
            base.Dispose(disposing);
        }

        public static Expression<D> Build(bool tailCall, Action<LambdaBuilder<D>> lambdaBody)
        {
            var builder = new LambdaBuilder<D>() { TailCall = tailCall };
            lambdaBody(builder);
            return builder.Upcast<IExpressionBuilder<Expression<D>>, LambdaBuilder<D>>().Build();
        }

        /// <summary>
        /// Constructs lambda expression from expression tree.
        /// </summary>
        /// <param name="lambdaBody">Lambda expression builder.</param>
        /// <returns>Constructed lambda expression.</returns>
        public static Expression<D> Build(Action<LambdaBuilder<D>> lambdaBody)
            => Build(false, lambdaBody);
    }
}