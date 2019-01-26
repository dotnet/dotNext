using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using TaskAwaiter = System.Runtime.CompilerServices.TaskAwaiter;

namespace DotNext.Metaprogramming
{
    using static Reflection.Types;
    using Runtime.CompilerServices;

    /// <summary>
    /// Represents <see langword="await"/> expression.
    /// </summary>
    public sealed class AwaitExpression : Expression
    {
        private sealed class AwaitChecker: ExpressionVisitor
        {
            internal bool ContainsAwait
            {
                get;
                private set;
            }

            protected override Expression VisitExtension(Expression node)
            {
                if(node is AwaitExpression)
                    ContainsAwait = true;
                return base.VisitExtension(node);
            }
        }

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

        internal static bool ContainsAwait(Expression node)
        {
            var checker = new AwaitChecker();
            checker.Visit(node);
            return checker.ContainsAwait;
        }

        internal MethodCallExpression GetAwaiter { get; }

        internal Type AwaiterType => GetAwaiter.Type;

        internal MethodInfo GetResultMethod { get; }

        /// <summary>
        /// Gets result type of asynchronous operation.
        /// </summary>
        public override Type Type => GetResultMethod.ReturnType;

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
        public override Expression Reduce() => GetAwaiter.Call(GetResultMethod);

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var expression = visitor.Visit(GetAwaiter.Object);
            return ReferenceEquals(expression, GetAwaiter.Object) ? this : new AwaitExpression(expression);
        }

        internal MethodCallExpression Reduce(ParameterExpression awaiterHolder, int state, LabelTarget stateLabel, LabelTarget returnLabel, CodeInsertionPoint inserter)
        {
            inserter(Assign(awaiterHolder, GetAwaiter));
            inserter(new TransitionExpression(awaiterHolder, state));
            inserter(Return(returnLabel));
            inserter(stateLabel.LandingSite());
            return awaiterHolder.Call(GetResultMethod);
        }
    }
}
