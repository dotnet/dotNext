using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    public abstract class LambdaBuilder: ExpressionBuilder
    {
        private ParameterExpression lambdaResult;
        private LabelTarget returnLabel;

        private protected LambdaBuilder(ExpressionBuilder parent = null)
            : base(parent)
        {
        }

        public sealed override Expression Body
        {
            set
            {
                returnLabel = null;
                base.Body = value;
            }
        }
        
        /// <summary>
        /// Gets return type of the lambda function.
        /// </summary>
        public abstract Type ReturnType { get; }

        /// <summary>
        /// Gets lambda parameters.
        /// </summary>
        public abstract ReadOnlyCollection<ParameterExpression> Parameters { get; }

        private protected abstract LambdaExpression Build(Expression body, bool tailCall);

        internal sealed override Expression Build()
        {
            if (!(returnLabel is null))
                AddStatement(returnLabel.LandingSite());
            //last instruction should be always a result of a function
            if (!(lambdaResult is null))
                AddStatement(lambdaResult);
            return Build(base.Build(), TailCall);
        }

        /// <summary>
        /// Gets lambda function result holder;
        /// </summary>
        public ParameterExpression Result
        {
            get
            {
                if (lambdaResult is null)
                    lambdaResult = DeclareVariable(ReturnType, NextName("lambdaResult_"));
                return lambdaResult;
            }
        }

        internal Expression Return(Expression result, bool addAsStatement)
        {
            if (returnLabel is null)
                returnLabel = Expression.Label("leave");
            result = ReturnType == typeof(void) ? returnLabel.Return().Upcast<Expression, GotoExpression>() : Expression.Block(Expression.Assign(Result, result), returnLabel.Return());
            return addAsStatement ? AddStatement(result) : result;
        }

        internal Expression Return(bool addAsStatement) => Return(ReturnType.Default(), addAsStatement);

        public Expression Return(ExpressionView result) => Return(result, true);

        public Expression Return() => Return(true);

        public bool TailCall { private get; set; }
    }

    public sealed class LambdaBuilder<D>: LambdaBuilder, IExpressionBuilder<Expression<D>>
        where D: Delegate
    {
        internal LambdaBuilder(ExpressionBuilder parent = null)
            : base(parent)
        {
            if(typeof(D).IsAbstract)
                throw new GenericArgumentException<D>("Delegate type should not be abstract", nameof(D));
            var invokeMethod = Delegates.GetInvokeMethod<D>();
            Parameters = new ReadOnlyCollection<ParameterExpression>((from parameter in invokeMethod.GetParameters() select Expression.Parameter(parameter.ParameterType, parameter.Name)).ToList());
            ReturnType = invokeMethod.ReturnType;
        } 

        /// <summary>
        /// Gets lambda parameters.
        /// </summary>
        public override ReadOnlyCollection<ParameterExpression> Parameters { get; }

        /// <summary>
        /// Gets return type of the lambda function.
        /// </summary>
        public override Type ReturnType { get; }

        private protected override LambdaExpression Build(Expression body, bool tailCall)
            => Expression.Lambda<D>(body, tailCall, Parameters);

        Expression<D> IExpressionBuilder<Expression<D>>.Build() => (Expression<D>)base.Build();

        public static Expression<D> Build(Action<LambdaBuilder<D>> lambdaBody)
        {
            var builder = new LambdaBuilder<D>();
            lambdaBody(builder);
            return builder.Upcast<IExpressionBuilder<Expression<D>>, LambdaBuilder<D>>().Build();
        }
    }
}