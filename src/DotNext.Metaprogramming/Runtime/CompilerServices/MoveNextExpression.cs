using System;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Runtime.CompilerServices
{
    using static Linq.Expressions.ExpressionBuilder;

    internal sealed class MoveNextExpression : TransitionExpression
    {
        private readonly uint stateId;
        private readonly Expression awaiter;

        internal MoveNextExpression(Expression awaiter, uint stateId)
            : base(stateId)
        {
            this.stateId = stateId;
            this.awaiter = awaiter;
        }

        public override Type Type => typeof(bool);

        public override Expression Reduce() => awaiter;

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var newAwaiter = visitor.Visit(awaiter);
            return ReferenceEquals(awaiter, newAwaiter) ? this : new MoveNextExpression(newAwaiter, stateId);
        }

        internal override Expression Reduce(ParameterExpression stateMachine)
        {
            const BindingFlags PublicInstanceFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var genericParam = Type.MakeGenericMethodParameter(0).MakeByRefType();
            var moveNext = stateMachine.Type.GetMethod(nameof(AsyncStateMachine<ValueTuple>.MoveNext), 1, PublicInstanceFlags, null, new[] { genericParam, typeof(uint) }, null)!.MakeGenericMethod(awaiter.Type);
            return stateMachine.Call(moveNext, awaiter, StateId);
        }
    }
}
