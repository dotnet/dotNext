using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using TaskAwaiter = System.Runtime.CompilerServices.TaskAwaiter;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents <see langword="await"/> expression.
    /// </summary>
    public sealed class AwaitExpression : Expression
    {
        public AwaitExpression(Expression expression)
        {
            //expression type must have type with GetAwaiter() method
            const BindingFlags PublicInstanceMethod = BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
            var getAwaiter = expression.Type.GetMethod(nameof(Task.GetAwaiter), PublicInstanceMethod, Type.DefaultBinder, Array.Empty<Type>(), Array.Empty<ParameterModifier>());
            if (getAwaiter is null)
                throw new ArgumentException($"Type {expression.Type.FullName} should have GetAwaiter() instance public method");
            else
                GetAwaiter = expression.Call(getAwaiter);
            GetResultMethod = GetAwaiter.Type.GetMethod(nameof(TaskAwaiter.GetResult), PublicInstanceMethod, Type.DefaultBinder, Array.Empty<Type>(), Array.Empty<ParameterModifier>());
            if (GetResultMethod is null)
                throw new ArgumentException($"Method {getAwaiter.DeclaringType.FullName + ':' + getAwaiter.Name} returns invalid awaiter pattern");
        }

        internal MethodCallExpression GetAwaiter { get; }

        internal Type AwaiterType => GetAwaiter.Type;

        internal MethodInfo GetResultMethod { get; }

        /// <summary>
        /// Gets result type of asynchronous operation.
        /// </summary>
        public override Type Type => GetAwaiter.Object.Type;

        /// <summary>
        /// Always return <see langword="true"/>.
        /// </summary>
        public override bool CanReduce => true;

        /// <summary>
        /// Gets expression node type.
        /// </summary>
        /// <see cref="ExpressionType.Extension"/>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <summary>
        /// Produces call of GetResult method which allows to obtain
        /// result in synchronous manner.
        /// </summary>
        /// <returns>Method call expression.</returns>
        public override Expression Reduce() => GetAwaiter.Object;

        protected override Expression VisitChildren(ExpressionVisitor visitor)
            => visitor.Visit(Reduce());
    }
}
