using System;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct StateTransition : IEquatable<StateTransition>
    {
        internal readonly LabelTarget? Successful;
        internal readonly LabelTarget? Failure;

        internal StateTransition(LabelTarget? successful, LabelTarget? failed)
        {
            if (successful is null && failed is null)
                throw new ArgumentNullException(nameof(successful));
            Successful = successful;
            Failure = failed;
        }

        internal void Deconstruct(out LabelTarget? successful, out LabelTarget? failed)
        {
            successful = Successful;
            failed = Failure;
        }

        internal Expression MakeGoto()
        {
            if (Failure is null)
                return Expression.Goto(Successful);
            else if (Successful is null)
                return Expression.Goto(Failure);
            else
                return Expression.Condition(new HasNoExceptionExpression(), Expression.Goto(Successful), Expression.Goto(Failure));
        }

        internal bool Equals(in StateTransition other)
            => Equals(Successful, other.Successful) && Equals(Failure, other.Failure);

        bool IEquatable<StateTransition>.Equals(StateTransition other) => Equals(in other);

        public override bool Equals(object? other) => other is StateTransition transition && Equals(transition);

        public override int GetHashCode() => HashCode.Combine(Successful, Failure);

        public static bool operator ==(in StateTransition first, in StateTransition second) => first.Equals(second);

        public static bool operator !=(in StateTransition first, in StateTransition second) => !first.Equals(second);
    }
}