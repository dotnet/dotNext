using System;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Metaprogramming
{
    using static Reflection.Types;
    using Runtime.CompilerServices;

    /// <summary>
    /// Represents async state machine transition.
    /// </summary>
    internal sealed class TransitionExpression: Expression
    {
        internal readonly Expression Awaiter;
        internal readonly int StateId;

        internal TransitionExpression(Expression awaiter, int stateId)
        {
            Awaiter = awaiter;
            StateId = stateId;
        }

        public override bool CanReduce => true;
        public override Type Type => typeof(void);
        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Expression Reduce() => Awaiter;
        internal MethodCallExpression Reduce(ParameterExpression stateMachine)
        {
            const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var moveNext = stateMachine.Type.GetMethod(nameof(IAsyncStateMachine<ValueTuple>.MoveNext), PublicInstance, 1, null, typeof(int)).MakeGenericMethod(Awaiter.Type);
            return stateMachine.Call(moveNext, Awaiter, StateId.AsConst());
        }
    }
}