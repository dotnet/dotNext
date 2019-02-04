using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Metaprogramming.Expressions;

    internal sealed class GuardedCodeContext: IGuardedCodeContext
    {
        internal readonly RecoveryCodeContext RecoveryContext;
        private readonly EnterGuardedCodeExpression enterGuardedCode;
        private readonly ExitGuardedCodeExpression exitGuardedCode;
        internal readonly LabelTarget FaultLabel;

        internal GuardedCodeContext(uint previousStateId, uint tryStateId, LabelTarget faultLabel)
        {
            enterGuardedCode = new EnterGuardedCodeExpression(tryStateId);
            exitGuardedCode = new ExitGuardedCodeExpression(previousStateId);
            FaultLabel = faultLabel;
        }

        internal GuardedCodeContext(uint previousStateId, uint tryStateId, LabelTarget faultLabel, uint catchStateId, LabelTarget finallyLabel)
        {
            enterGuardedCode = new EnterGuardedCodeExpression(tryStateId);
            exitGuardedCode = new ExitGuardedCodeExpression(previousStateId);
            FaultLabel = faultLabel;
            RecoveryContext = new RecoveryCodeContext(catchStateId, finallyLabel);
        }

        LabelTarget IGuardedCodeContext.FaultLabel => FaultLabel;

        internal BlockExpression MakeTryBody(Expression @try)
            => RecoveryContext is null ?
                @try.AddPrologue(false, enterGuardedCode) :
                @try.AddPrologue(false, enterGuardedCode).AddEpilogue(false, RecoveryContext.FaultLabel.Goto(), FaultLabel.LandingSite());

        private LabelTarget FinallyLabel => RecoveryContext is null ? FaultLabel : RecoveryContext.FaultLabel;

        internal BlockExpression MakeFaultBody(Expression fault, StateTransitionTable stateSwitchTable, ExpressionVisitor visitor)
        {
            fault = visitor.Visit(fault);
            stateSwitchTable.RegisterFaultTransition(enterGuardedCode, this);
            return fault is null ?
                Expression.Block(typeof(void), FinallyLabel.LandingSite(), exitGuardedCode, new RethrowExpression()) :
                fault.AddPrologue(false, FinallyLabel.LandingSite(), exitGuardedCode).AddEpilogue(false, new RethrowExpression());
        }
    }
}