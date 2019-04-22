using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using static Collections.Generic.Collection;
    using static Reflection.DelegateType;

    /// <summary>
    /// Represents lambda function builder.
    /// </summary>
    internal abstract class LambdaScope: LexicalScope, ICompoundStatement<Action<LambdaContext>>
    {
        private protected bool tailCall;

        private protected LambdaScope(LexicalScope parent, bool tailCall) : base(parent) => this.tailCall = tailCall;

        private protected IReadOnlyList<ParameterExpression> GetParameters(System.Reflection.ParameterInfo[] parameters)
            =>  Array.ConvertAll(parameters, parameter => Expression.Parameter(parameter.ParameterType, parameter.Name));

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

        internal abstract Expression Return(Expression result);

        void ICompoundStatement<Action<LambdaContext>>.ConstructBody(Action<LambdaContext> body)
        {
            using (var context = new LambdaContext(this))
                body(context);
        }
    }

    /// <summary>
    /// Represents lambda function builder.
    /// </summary>
    /// <typeparam name="D">The delegate describing signature of lambda function.</typeparam>
    internal sealed class LambdaScope<D>: LambdaScope, IExpressionBuilder<Expression<D>>
        where D: Delegate
    {
        private ParameterExpression recursion;
        private ParameterExpression lambdaResult;
        private LabelTarget returnLabel;

        internal LambdaScope(LexicalScope parent = null, bool tailCall = false)
            : base(parent, tailCall)
        {
            if (typeof(D).IsAbstract)
                throw new GenericArgumentException<D>(ExceptionMessages.AbstractDelegate, nameof(D));
            var invokeMethod = GetInvokeMethod<D>();
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
                    DeclareVariable(lambdaResult = Expression.Variable(ReturnType, "result"));
                return lambdaResult;
            }
        }

        internal override Expression Return(Expression result)
        {
            if (returnLabel is null)
                returnLabel = Expression.Label("leave");
            if (result is null)
                result = ReturnType.AsDefault();
            result = ReturnType == typeof(void) ? (Expression)returnLabel.Return() : Expression.Block(Expression.Assign(Result, result), returnLabel.Return());
            return result;
        }

        public Expression<D> Build()
        {
            var body = base.Build();
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
                locals = Enumerable.Empty<ParameterExpression>();
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

        public override void Dispose()
        {
            lambdaResult = null;
            returnLabel = null;
            recursion = null;
            base.Dispose();
        }
    }
}