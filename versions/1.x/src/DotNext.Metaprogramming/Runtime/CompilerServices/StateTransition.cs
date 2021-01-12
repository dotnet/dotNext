using System;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct StateTransition : IEquatable<StateTransition>
    {
        internal readonly LabelTarget Successful;
        internal readonly LabelTarget Failure;

        internal StateTransition(LabelTarget successful, LabelTarget failed)
        {
            Successful = successful;
            Failure = failed;
        }

        internal void Deconstruct(out LabelTarget successful, out LabelTarget failed)
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

        public override bool Equals(object other) => other is StateTransition transition && Equals(transition);

        public override int GetHashCode()
        {
            if (Successful is null)
                return Failure is null ? 0 : Failure.GetHashCode();
            else if (Failure is null)
                return Successful.GetHashCode();
            else
            {
                var hashCode = 237146532;
                hashCode = hashCode * -1521134295 + Successful.GetHashCode();
                hashCode = hashCode * -1521134295 + Failure.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(in StateTransition first, in StateTransition second) => first.Equals(second);
        public static bool operator !=(in StateTransition first, in StateTransition second) => !first.Equals(second);
    }
}