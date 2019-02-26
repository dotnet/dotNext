using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Runtime.CompilerServices;
    using Reflection;

    /// <summary>
    /// Represents return from asynchronous lambda function.
    /// </summary>
    /// <remarks>
    /// This expression turns async state machine into final state.
    /// </remarks>
    /// <see cref="AsyncLambdaBuilder{D}"/>
    public sealed class AsyncResultExpression: Expression
    {
        /// <summary>
        /// Constructs non-void return from asynchronous lambda function.
        /// </summary>
        /// <param name="result">An expression representing result to be returned from asynchronous lambda function.</param>
        public AsyncResultExpression(Expression result)
            => AsyncResult = result;

        /// <summary>
        /// Constructs void return from asynchronous lambda function.
        /// </summary>
        public AsyncResultExpression()
            : this(Empty())
        {
        }

        /// <summary>
        /// An expression representing result to be returned from asynchronous lambda function.
        /// </summary>
        public Expression AsyncResult { get; }

        /// <summary>
        /// Type of this expression.
        /// </summary>
        /// <remarks>
        /// The type of this expression is <see cref="Task"/> or derived class.
        /// </remarks>
        public override Type Type => AsyncResult.Type.MakeTaskType();

        /// <summary>
        /// Expression type. Always returns <see cref="ExpressionType.Extension"/>.
        /// </summary>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <summary>
        /// Indicates that this expression can be reduced to well-known LINQ expression.
        /// </summary>
        public override bool CanReduce => true;

        /// <summary>
        /// Reduces this expression to the well-known LINQ expression.
        /// </summary>
        /// <returns>The reduced expression.</returns>
        public override Expression Reduce()
        {
            const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
            Expression completedTask, failedTask;
            var catchedException = Variable(typeof(Exception));
            if (AsyncResult.Type == typeof(void))
            {
                completedTask = Block(AsyncResult, Property(null, typeof(Task).GetProperty(nameof(Task.CompletedTask))));
                failedTask = Call(null, typeof(Task).GetMethod(nameof(Task.FromException), PublicStatic, 0L, typeof(Exception)), catchedException);
            }
            else
            {
                completedTask = Call(null, typeof(Task).GetMethod(nameof(Task.FromResult), PublicStatic, 1L, new Type[] { null }).MakeGenericMethod(AsyncResult.Type), AsyncResult);
                failedTask = Call(null, typeof(Task).GetMethod(nameof(Task.FromException), PublicStatic, 1L, typeof(Exception)).MakeGenericMethod(AsyncResult.Type), catchedException);
            }
            return AsyncResult is ConstantExpression || AsyncResult is DefaultExpression ?
                completedTask :
                TryCatch(completedTask, Catch(catchedException, failedTask));
        }

        internal Expression Reduce(ParameterExpression stateMachine, LabelTarget endOfAsyncMethod)
        {
            //if state machine is non-void then use Result property
            var resultProperty = stateMachine.Type.GetProperty(nameof(AsyncStateMachine<ValueTuple, int>.Result));
            if (!(resultProperty is null))
                return Block(Property(stateMachine, resultProperty).Assign(AsyncResult), endOfAsyncMethod.Return());
            //else, just call Complete method
            return Block(AsyncResult, stateMachine.Call(nameof(AsyncStateMachine<ValueTuple>.Complete)), endOfAsyncMethod.Return());
        }

        /// <summary>
        /// Visit children expressions.
        /// </summary>
        /// <param name="visitor">Expression visitor.</param>
        /// <returns>Potentially modified expression if one of children expressions is modified during visit.</returns>
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var expression = visitor.Visit(AsyncResult);
            return ReferenceEquals(expression, AsyncResult) ? this : new AsyncResultExpression(expression);
        }
    }
}
