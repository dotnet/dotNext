using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DotNext.Linq.Expressions
{
    using Runtime.CompilerServices;

    /// <summary>
    /// Represents suspension point in the execution of the lambda function until the awaited task completes.
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/await">Await expression</seealso>
    public sealed class AwaitExpression : CustomExpression
    {
        private static readonly UserDataSlot<bool> IsAwaiterVarSlot = UserDataSlot<bool>.Allocate();

        /// <summary>
        /// Constructs <see langword="await"/> expression.
        /// </summary>
        /// <param name="expression">An expression providing asynchronous result in the form or <see cref="Task"/> or any other TAP pattern.</param>
        /// <param name="configureAwait"><see langword="true"/> to call <see cref="Task.ConfigureAwait(bool)"/> with <see langword="false"/> argument.</param>
        /// <exception cref="ArgumentException">Passed expression doesn't implement TAP pattern.</exception>
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(Task))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(Task<>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(ValueTask))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(ValueTask<>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(TaskAwaiter))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(TaskAwaiter<>))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(ValueTaskAwaiter))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(ValueTaskAwaiter<>))]
        public AwaitExpression(Expression expression, bool configureAwait = false)
        {
            const BindingFlags PublicInstanceMethod = BindingFlags.Public | BindingFlags.Instance;
            if (configureAwait)
            {
                MethodInfo? configureMethod = expression.Type.GetMethod(nameof(Task.ConfigureAwait), PublicInstanceMethod, Type.DefaultBinder, new[] { typeof(bool) }, null);
                if (configureMethod is not null)
                    expression = expression.Call(configureMethod, false.Const());
            }

            // expression type must have type with GetAwaiter() method
            MethodInfo? getAwaiter = expression.Type.GetMethod(nameof(Task.GetAwaiter), PublicInstanceMethod, Type.DefaultBinder, Array.Empty<Type>(), null);
            GetAwaiter = expression.Call(getAwaiter ?? throw new ArgumentException(ExceptionMessages.MissingGetAwaiterMethod(expression.Type)));
            getAwaiter = GetAwaiter.Type.GetMethod(nameof(TaskAwaiter.GetResult), PublicInstanceMethod, Type.DefaultBinder, Array.Empty<Type>(), null);
            GetResultMethod = getAwaiter ?? throw new ArgumentException(ExceptionMessages.MissingGetResultMethod(GetAwaiter.Type));
        }

        internal ParameterExpression NewAwaiterHolder()
        {
            var result = Variable(AwaiterType);
            result.GetUserData().Set(IsAwaiterVarSlot, true);
            return result;
        }

        internal static bool IsAwaiterHolder([NotNullWhen(true)] ParameterExpression? variable)
            => variable?.GetUserData().Get(IsAwaiterVarSlot) ?? false;

        internal MethodCallExpression GetAwaiter { get; }

        internal Type AwaiterType => GetAwaiter.Type;

        internal MethodInfo GetResultMethod { get; }

        /// <summary>
        /// Gets result type of asynchronous operation.
        /// </summary>
        public override Type Type => GetResultMethod.ReturnType;

        /// <summary>
        /// Translates this expression into predefined set of expressions
        /// using Lowering technique.
        /// </summary>
        /// <returns>Translated expression.</returns>
        public override Expression Reduce() => GetAwaiter.Call(GetResultMethod);

        /// <summary>
        /// Visit children expressions.
        /// </summary>
        /// <param name="visitor">Expression visitor.</param>
        /// <returns>Potentially modified expression if one of children expressions is modified during visit.</returns>
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            Debug.Assert(GetAwaiter.Object is not null);
            var expression = visitor.Visit(GetAwaiter.Object);
            return ReferenceEquals(expression, GetAwaiter.Object) ? this : new AwaitExpression(expression);
        }

        internal MethodCallExpression Reduce(ParameterExpression awaiterHolder, uint state, LabelTarget stateLabel, LabelTarget returnLabel, CodeInsertionPoint prologue)
        {
            prologue(Assign(awaiterHolder, GetAwaiter));
            prologue(Condition(new MoveNextExpression(awaiterHolder, state), Empty(), Return(returnLabel), typeof(void)));
            prologue(stateLabel.LandingSite());
            return awaiterHolder.Call(GetResultMethod);
        }
    }
}
