using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    internal class GuardedCodeContext: IGuardedCodeContext
    {
        private protected readonly EnterGuardedCodeExpression enterGuardedCode;
        private protected readonly ExitGuardedCodeExpression exitGuardedCode;
        internal readonly LabelTarget FaultLabel;
        
        private protected GuardedCodeContext(uint previousState, uint guardedState, LabelTarget faultLabel)
        {
            enterGuardedCode = new EnterGuardedCodeExpression(guardedState);
            exitGuardedCode = new ExitGuardedCodeExpression(previousState);
            FaultLabel = faultLabel;
        }

        private protected void RegisterFaultTransition(IDictionary<uint, StateTransition> stateSwitchTable)
        {
            if (!stateSwitchTable.ContainsKey(enterGuardedCode.StateId))
                stateSwitchTable.Add(enterGuardedCode.StateId, new StateTransition(null, FaultLabel));
        }

        LabelTarget IGuardedCodeContext.FaultLabel => FaultLabel;
    }
}
