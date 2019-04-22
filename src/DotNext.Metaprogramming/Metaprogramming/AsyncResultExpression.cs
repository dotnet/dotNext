using System;
using System.Threading.Tasks;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Threading.Tasks;
    using Runtime.CompilerServices;

    /// <summary>
    /// Represents return from asynchronous lambda function.
    /// </summary>
    /// <remarks>
    /// This expression turns async state machine into final state.
    /// </remarks>
    /// <see cref="AsyncLambdaScope{D}"/>
    public sealed class AsyncResultExpression: Expression
    {
        private readonly TaskType taskType;
        
        internal AsyncResultExpression(Expression result, TaskType taskType)
        {
            this.taskType = taskType;
            AsyncResult = result;
        }

        internal AsyncResultExpression(TaskType taskType)
            : this(taskType.ResultType.AsDefault(), taskType)
        {
        }

        /// <summary>
        /// Constructs non-void return from asynchronous lambda function.
        /// </summary>
        /// <param name="result">An expression representing result to be returned from asynchronous lambda function.</param>
        /// <param name="valueTask"><see langword="true"/>, to represent the result as <see cref="ValueTask"/> or <see cref="ValueTask{TResult}"/>.</param>
        public AsyncResultExpression(Expression result, bool valueTask)
        {
            AsyncResult = result;
            taskType = new TaskType(result.Type, valueTask);
        }

        /// <summary>
        /// Constructs void return from asynchronous lambda function.
        /// </summary>
        /// <param name="valueTask"><see langword="true"/>, to represent the result as <see cref="ValueTask"/>.</param>
        public AsyncResultExpression(bool valueTask)
            : this(Empty(), valueTask)
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
        /// The type of this expression is <see cref="Task"/>, <see cref="Task{TResult}"/>, <see cref="ValueTask"/> or <see cref="ValueTask{TResult}"/>.
        /// </remarks>
        public override Type Type => taskType;

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
            Expression completedTask, failedTask;
            var catchedException = Variable(typeof(Exception));
            if (AsyncResult.Type == typeof(void))
            {
                completedTask = Block(AsyncResult, typeof(CompletedTask).AsDefault());
                failedTask = typeof(CompletedTask).New(catchedException);
            }
            else
            {
                completedTask = typeof(CompletedTask<>).MakeGenericType(AsyncResult.Type).New(AsyncResult);
                failedTask = completedTask.Type.New(catchedException);
            }
            return AsyncResult is ConstantExpression || AsyncResult is DefaultExpression ?
                completedTask.Convert(taskType) :
                TryCatch(completedTask, Catch(catchedException, failedTask)).Convert(taskType);
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
            return ReferenceEquals(expression, AsyncResult) ? this : new AsyncResultExpression(expression, taskType);
        }
    }
}
