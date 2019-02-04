using System.Collections.Generic;

namespace DotNext.Runtime.CompilerServices
{
    internal sealed class StateTransitionTable: SortedDictionary<uint, StateTransition>
    {
        internal void RegisterFaultTransition(TransitionExpression transition, IGuardedCodeContext context)
        {
            if (!ContainsKey(transition.StateId))
                Add(transition.StateId, new StateTransition(null, context.FaultLabel));
        }
    }
}
