using System;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Runtime.CompilerServices
{
    using static Linq.Expressions.ExpressionBuilder;
    using static Reflection.TypeExtensions;

    internal sealed class MoveNextExpression : TransitionExpression
    {
        private new readonly uint StateId;
        private readonly Expression awaiter;

        internal MoveNextExpression(Expression awaiter, uint stateId)
            : base(stateId)
        {
            StateId = stateId;
            this.awaiter = awaiter;
        }

        public override Type Type => typeof(bool);
        public override Expression Reduce() => awaiter;
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var newAwaiter = visitor.Visit(awaiter);
            return ReferenceEquals(awaiter, newAwaiter) ? this : new MoveNextExpression(newAwaiter, StateId);
        }

        internal override Expression Reduce(ParameterExpression stateMachine)
        {
            const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var moveNext = stateMachine.Type.GetMethod(nameof(AsyncStateMachine<ValueTuple>.MoveNext), PublicInstance, 1, null, typeof(uint)).MakeGenericMethod(awaiter.Type);
            return stateMachine.Call(moveNext, awaiter, base.StateId);
        }
    }
}
