using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using static System.Diagnostics.Debug;

namespace DotNext.Runtime.CompilerServices
{
    using static Metaprogramming.Expressions;

    internal sealed class TryCatchCodeContext: GuardedCodeContext
    {
        internal readonly RecoveryCodeContext RecoveryContext;

        internal TryCatchCodeContext(uint previousStateId, uint tryStateId, LabelTarget faultLabel)
            : base(previousStateId, tryStateId, faultLabel)
        {
        }

        internal TryCatchCodeContext(uint previousStateId, uint tryStateId, LabelTarget faultLabel, uint catchStateId, LabelTarget finallyLabel)
            : base(previousStateId, tryStateId, faultLabel)
        {
            RecoveryContext = new RecoveryCodeContext(previousStateId, catchStateId, finallyLabel);
        }

        internal BlockExpression MakeTryBody(Expression @try)
            => RecoveryContext is null ?
                @try.AddPrologue(false, enterGuardedCode) :
                @try.AddPrologue(false, enterGuardedCode).AddEpilogue(false, RecoveryContext.FaultLabel.Goto(), FaultLabel.LandingSite());

        private LabelTarget FinallyLabel => RecoveryContext is null ? FaultLabel : RecoveryContext.FaultLabel;

        internal BlockExpression MakeFaultBody(Expression fault, IDictionary<uint, StateTransition> stateSwitchTable, ExpressionVisitor visitor)
        {
            RegisterFaultTransition(stateSwitchTable);
            return fault is null ?
                Expression.Block(typeof(void), FinallyLabel.LandingSite(), exitGuardedCode, new RethrowExpression()) :
                Expression.Block(typeof(void), FinallyLabel.LandingSite(), exitGuardedCode, fault, new RethrowExpression());
        }
    }
}