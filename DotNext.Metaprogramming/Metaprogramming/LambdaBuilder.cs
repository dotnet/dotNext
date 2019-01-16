using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    public sealed class LambdaBuilder<D>: ExpressionBuilder, IExpressionBuilder<Expression<D>>
        where D: Delegate
    {
        private LabelTarget returnLabel;

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
        public ReadOnlyCollection<ParameterExpression> Parameters { get; }

        /// <summary>
        /// Gets return type of the lambda function.
        /// </summary>
        public Type ReturnType { get; }

        public override Expression Body
        {
            set
            {
                returnLabel = null;
                base.Body = value;
            }
        }

        public void Return(Expression result)
        {
            if(returnLabel is null)
                returnLabel = Expression.Label(ReturnType, "leave");
            AddStatement(returnLabel.Return(result));
        }

        public void Return()
        {
            if(returnLabel is null)
                returnLabel = Expression.Label("leave");
            AddStatement(returnLabel.Return());
        }

        public bool TailCall { private get; set; }

        internal override Expression Build() => this.Upcast<IExpressionBuilder<Expression<D>>, LambdaBuilder<D>>().Build();    

        Expression<D> IExpressionBuilder<Expression<D>>.Build()
        {
            if (!(returnLabel is null))
                AddStatement(returnLabel.LandingSite(Expression.Default(ReturnType)));
            return Expression.Lambda<D>(base.Build(), TailCall, Parameters);
        }

        public static Expression<D> Build(Action<LambdaBuilder<D>> lambdaBody)
        {
            var builder = new LambdaBuilder<D>();
            lambdaBody(builder);
            return builder.Upcast<IExpressionBuilder<Expression<D>>, LambdaBuilder<D>>().Build();
        }
    }
}