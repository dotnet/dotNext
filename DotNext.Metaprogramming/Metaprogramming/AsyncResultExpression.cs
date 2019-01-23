using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Reflection;

    public sealed class AsyncResultExpression: Expression
    {
        public AsyncResultExpression(Expression result)
            => AsyncResult = result;

        public AsyncResultExpression()
            : this(Empty())
        {
        }

        public Expression AsyncResult { get; }
        public override Type Type => AsyncResult.Type.MakeTaskType();
        public override ExpressionType NodeType => ExpressionType.Extension;
        public override bool CanReduce => true;
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
    }
}
