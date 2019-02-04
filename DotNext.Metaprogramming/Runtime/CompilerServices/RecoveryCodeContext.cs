using System;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Metaprogramming.Expressions;

    internal sealed class RecoveryCodeContext : IGuardedCodeContext
    {
        internal readonly LabelTarget FaultLabel;
        private readonly uint recoveryState;

        internal RecoveryCodeContext(uint guardedState, LabelTarget faultLabel) 
        {
            FaultLabel = faultLabel;
            recoveryState = guardedState;
        }

        LabelTarget IGuardedCodeContext.FaultLabel => FaultLabel;

        internal ConditionalExpression MakeCatchBlock(CatchBlock @catch, StateTransitionTable stateSwitchTable, ExpressionVisitor visitor)
        {
            var recovery = new RecoverFromExceptionExpression(recoveryState, @catch.Variable);
            stateSwitchTable.RegisterFaultTransition(recovery, this);
            @catch = @catch.Filter is null ?
                @catch.Update(@catch.Variable, recovery, @catch.Body) :
                @catch.Update(@catch.Variable, recovery.AndAlso(@catch.Filter), @catch.Body);
            var filter = visitor.Visit(@catch.Filter);
            if (VisitorContext.ContainsAwait(filter))
                throw new NotSupportedException("Filter of catch block cannot contain await expressions");
            var handler = visitor.Visit(@catch.Body);
            handler = handler.AddEpilogue(false, FaultLabel.Goto());
            return Expression.IfThen(filter, handler);
        }
    }
}
